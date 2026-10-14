using System;
using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using UnityEngine;
using UnityEngine.Tilemaps;
using TileAsset = UnityEngine.Tilemaps.Tile;   // Rewind.Core has a Tile too

namespace Rewind.Gameplay
{
    /// <summary>
    /// Shows the current level. It instantiates tile prefabs when a level loads and then mirrors the game state
    /// every frame, including the rewind playback. Cell (x, y) sits at local (x + 0.5, -(y + 0.5)).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] RewindGame game;
        [SerializeField] BoardPalette palette;
        [SerializeField] BoardPrefabSet prefabs;

        [Header("Layers (children of this object)")]
        [SerializeField, Tooltip("Shaken when an echo vanishes.")] Transform shakeRoot;
        [SerializeField] Transform tileRoot;
        [SerializeField] Transform pieceRoot;
        [SerializeField] Transform markerRoot;
        [SerializeField] Transform fxRoot;
        [SerializeField] RewindOverlay rewindOverlay;

        [Header("Feel")]
        [SerializeField] float shakeAmount = 0.12f;
        [SerializeField] float shakeDecay = 6f;
        [SerializeField] int paradoxParticles = 30;
        [SerializeField] int crumbleParticles = 14;
        [SerializeField] int solveParticles = 40;

        [Header("Plate wires")]
        [SerializeField, Tooltip("Thickness, in tiles, of the wires drawn from plates to the doors they control.")] float wireWidth = 0.08f;
        [SerializeField, Range(0f, 1f)] float wireIdleAlpha = 0.18f;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha of a wire while its plate is pressed.")] float wireLitAlpha = 0.75f;

        /// <summary>Raised after the board is rebuilt, with its bounds in world space.</summary>
        public event Action<Bounds> LayoutChanged;

        readonly List<PlateView> plates = new List<PlateView>();
        readonly List<DoorView> doors = new List<DoorView>();
        readonly Dictionary<int, GroundView> grounds = new Dictionary<int, GroundView>();
        readonly List<BodyView> echoViews = new List<BodyView>();
        readonly List<CrateView> crateViews = new List<CrateView>();
        readonly List<ParadoxMarkerView> markers = new List<ParadoxMarkerView>();
        readonly List<SpriteRenderer> trailDots = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> chevrons = new List<SpriteRenderer>();
        readonly List<SpriteBurst> bursts = new List<SpriteBurst>();
        readonly List<FloatingLabel> labels = new List<FloatingLabel>();
        readonly Dictionary<Vector2Int, int> stackCounts = new Dictionary<Vector2Int, int>();
        readonly List<(SpriteRenderer renderer, int plate, Color color)> wires = new List<(SpriteRenderer, int, Color)>();
        readonly List<(SpriteRenderer renderer, int checkpoint)> checkpointViews = new List<(SpriteRenderer, int)>();
        readonly List<GameObject> closedShades = new List<GameObject>();
        Sprite whiteSprite;

        // Floors and walls go on two tilemaps instead of an object per cell, so even big levels build quickly.
        Tilemap floorMap, wallMap;
        Sprite plainFloor;
        readonly Dictionary<Sprite, TileAsset> tileAssets = new Dictionary<Sprite, TileAsset>();
        readonly List<TileChangeData> floorChanges = new List<TileChangeData>();
        readonly List<TileChangeData> wallChanges = new List<TileChangeData>();
        BodyView playerView;
        LevelLayout layout;
        bool[] platePressed = Array.Empty<bool>();
        bool[][] doorPips = Array.Empty<bool[]>();
        float shake;

        public Bounds WorldBounds { get; private set; }
        public BoardPalette Palette => palette;
        public RewindGame Game => game;

        /// <summary>World-space bounds of a block of cells, e.g. one room.</summary>
        public Bounds CellBounds(RectInt cells) =>
            new Bounds(transform.TransformPoint(new Vector3(cells.x + cells.width * 0.5f, -(cells.y + cells.height * 0.5f), 0f)),
                new Vector3(cells.width, cells.height, 0f));
        public static Vector3 CellToLocal(Vector2 cell) => new Vector3(cell.x + 0.5f, -(cell.y + 0.5f), 0f);

        TerrainTileset Terrain => prefabs ? prefabs.terrain : null;
        Material terrainMaterial;
        GroundState[] holeGround;               // the ground being drawn this frame, read by IsHoleCell
        Func<int, int, bool> isHoleCell;        // cached so picking hole tiles every frame allocates nothing

        void OnEnable()
        {
            if (!game) return;
            game.LevelLoaded += OnLevelLoaded;
            game.LevelUnloaded += Clear;
            game.LoopReady += OnLoopReady;
            game.Ticked += OnTicked;
            game.ParadoxOccurred += OnParadox;
            game.Solved += OnSolved;
            game.SectionChanged += RefreshSections;
            if (game.HasLevel) { OnLevelLoaded(game.CurrentLevel, game.LevelIndex); OnLoopReady(game.LoopNumber); }
        }

        void OnDisable()
        {
            if (!game) return;
            game.LevelLoaded -= OnLevelLoaded;
            game.LevelUnloaded -= Clear;
            game.LoopReady -= OnLoopReady;
            game.Ticked -= OnTicked;
            game.ParadoxOccurred -= OnParadox;
            game.Solved -= OnSolved;
            game.SectionChanged -= RefreshSections;
        }

        // ---- Building ------------------------------------------------------------------------

        void OnLevelLoaded(LevelData level, int index)
        {
            Clear();
            layout = game.Session.Layout;
            var terrain = Terrain;
            terrainMaterial = terrain ? TerrainMaterial(terrain) : null;
            PrepareTilemaps(terrain);
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                    BuildCell(new Vector2Int(x, y), terrain);
            floorMap.SetTiles(floorChanges.ToArray(), false);
            wallMap.SetTiles(wallChanges.ToArray(), false);

            if (prefabs.startMarker)
            {
                var start = Instantiate(prefabs.startMarker, tileRoot);
                start.transform.localPosition = CellToLocal(layout.Start);
                if (start.TryGetComponent(out SpriteRenderer sr)) sr.color = palette.startMarker;
            }

            platePressed = new bool[layout.Plates.Count];
            doorPips = new bool[layout.Doors.Count][];
            for (int i = 0; i < layout.Doors.Count; i++)
                doorPips[i] = new bool[layout.DoorPlates(i).Count];
            BuildWires();
            RefreshSections();

            for (int i = 0; i < layout.CrateStarts.Count; i++)
            {
                var crate = Instantiate(prefabs.crate, pieceRoot);
                crate.Setup(palette);
                crateViews.Add(crate);
            }
            playerView = Instantiate(prefabs.player, pieceRoot);
            playerView.Setup(palette.player, 0, palette);

            var size = new Vector3(layout.Width, layout.Height, 0f);
            var local = new Bounds(new Vector3(layout.Width * 0.5f, -layout.Height * 0.5f, 0f), size);
            WorldBounds = new Bounds(transform.TransformPoint(local.center), size);
            if (rewindOverlay)
            {
                rewindOverlay.Fit(local, palette.rewind);
                rewindOverlay.SetVisible(false);
            }
            LayoutChanged?.Invoke(WorldBounds);
        }

        void BuildCell(Vector2Int cell, TerrainTileset terrain)
        {
            var tile = layout.TileAt(cell);
            var pos = CellToLocal(cell);
            if (tile.Kind == TileKind.Wall)
            {
                if (terrain)
                {
                    // Auto-tiled walls round off their outer corners, so inner walls need floor underneath to show
                    // through. Walls on the map edge get none, so the board's outer corners show the background.
                    if (!IsMapEdge(cell)) BuildFloor(cell, terrain);
                    PlaceWallTile(cell, terrain);
                    return;
                }
                var wall = Instantiate(prefabs.wall, tileRoot);
                wall.transform.localPosition = pos;
                bool openAbove = cell.y > 0 && !layout.IsWall(cell + Dir.Up);
                bool openBelow = cell.y < layout.Height - 1 && !layout.IsWall(cell + Dir.Down);
                wall.Setup(openAbove, openBelow, palette);
                return;
            }

            BuildFloor(cell, terrain);

            int index = layout.Index(cell);
            if (layout.InitialGround(index) != GroundState.Solid)
            {
                var ground = Instantiate(prefabs.ground, tileRoot);
                ground.transform.localPosition = pos;
                ground.Setup(cell, palette);
                ground.SetState(layout.InitialGround(index));
                grounds[index] = ground;
            }

            switch (tile.Kind)
            {
                case TileKind.Plate:
                {
                    var plate = Instantiate(prefabs.plate, tileRoot);
                    plate.transform.localPosition = pos;
                    plate.Setup(palette.Channel(tile.Channel));
                    plates.Add(plate);   // plates are created in map order, matching layout.Plates
                    break;
                }
                case TileKind.Door:
                {
                    var door = Instantiate(prefabs.door, tileRoot);
                    door.transform.localPosition = pos;
                    bool across = !layout.IsWall(cell + Dir.Left) && !layout.IsWall(cell + Dir.Right);
                    door.Setup(palette.Channel(tile.Channel), tile.Inverse, across, layout.DoorPlates(layout.DoorIndexAt(cell)).Count, palette);
                    doors.Add(door);     // same order as layout.Doors
                    break;
                }
                case TileKind.Checkpoint:
                {
                    if (!prefabs.startMarker) break;
                    var marker = Instantiate(prefabs.startMarker, tileRoot);
                    marker.transform.localPosition = pos;
                    if (marker.TryGetComponent(out SpriteRenderer sr)) checkpointViews.Add((sr, layout.CheckpointIndexAt(cell)));
                    break;
                }
                case TileKind.Exit:
                {
                    var exit = Instantiate(prefabs.exit, tileRoot);
                    exit.transform.localPosition = pos;
                    exit.Setup(palette.exit);
                    break;
                }
            }
        }

        // The floor checkerboard, using the tileset's floor sprites when there is a tileset.
        void BuildFloor(Vector2Int cell, TerrainTileset terrain)
        {
            var sprite = terrain ? terrain.FloorTile(cell) : null;
            if (!sprite) sprite = plainFloor;
            if (!sprite) return;
            var color = (cell.x + cell.y) % 2 == 1 ? palette.floorA : palette.floorB;
            var tile = TileFor(sprite, 1f);
            floorChanges.Add(new TileChangeData(TilePosition(cell), tile, color, tile.transform));
        }

        // A wall picks its tile from which of its eight neighbours are wall (see TerrainTileset).
        void PlaceWallTile(Vector2Int cell, TerrainTileset terrain)
        {
            var sprite = terrain.Tile(TerrainTileset.Kind.Wall, TerrainTileset.NeighbourMask(IsWallCell, cell.x, cell.y), palette);
            if (!sprite) return;
            // a hair of overlap so neighbouring walls never show a seam
            var tile = TileFor(sprite, 1.01f);
            wallChanges.Add(new TileChangeData(TilePosition(cell), tile, Color.white, tile.transform));
        }

        // Cell (x, y) sits at local (x + 0.5, -(y + 0.5)), which is tilemap cell (x, -y - 1).
        static Vector3Int TilePosition(Vector2Int cell) => new Vector3Int(cell.x, -cell.y - 1, 0);

        /// <summary>A tile showing a sprite scaled to fill exactly one cell, whatever the sprite's resolution. Made once per sprite.</summary>
        TileAsset TileFor(Sprite sprite, float scale)
        {
            if (tileAssets.TryGetValue(sprite, out var tile) && tile) return tile;
            tile = ScriptableObject.CreateInstance<TileAsset>();
            tile.hideFlags = HideFlags.DontSave;
            tile.sprite = sprite;
            var size = sprite.bounds.size;
            tile.transform = size.x > 0f && size.y > 0f ? Matrix4x4.Scale(new Vector3(scale / size.x, scale / size.y, 1f)) : Matrix4x4.identity;
            tile.flags = TileFlags.LockTransform;   // the colour stays free, for the floor checkerboard
            tileAssets[sprite] = tile;
            return tile;
        }

        /// <summary>Makes the floor and wall tilemaps the first time, beside the tile objects so they shake with them, and empties them.</summary>
        void PrepareTilemaps(TerrainTileset terrain)
        {
            if (!floorMap)
            {
                var grid = new GameObject("Tilemaps", typeof(Grid)).transform;
                grid.SetParent(tileRoot.parent, false);
                grid.localPosition = tileRoot.localPosition;
                var template = prefabs.floor ? prefabs.floor.GetComponentInChildren<SpriteRenderer>(true) : null;
                plainFloor = template ? template.sprite : null;
                floorMap = TilemapLayer(grid, "Floor", template ? template.sortingOrder : 0, template ? template.sharedMaterial : null);
                wallMap = TilemapLayer(grid, "Walls", terrain ? terrain.SortingOrder : 1, null);
            }
            if (terrainMaterial) wallMap.GetComponent<TilemapRenderer>().sharedMaterial = terrainMaterial;
            floorMap.ClearAllTiles();
            wallMap.ClearAllTiles();
            floorChanges.Clear();
            wallChanges.Clear();
        }

        static Tilemap TilemapLayer(Transform grid, string name, int order, Material material)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            if (material) renderer.sharedMaterial = material;
            return go.GetComponent<Tilemap>();
        }

        // A thin line from every plate to each door it controls, in the colour they share.
        void BuildWires()
        {
            for (int d = 0; d < layout.Doors.Count; d++)
            {
                var door = layout.Doors[d];
                var color = palette.Channel(door.Channel);
                foreach (int p in layout.DoorPlates(d))
                {
                    Vector3 a = CellToLocal(layout.Plates[p].Cell), b = CellToLocal(door.Cell), along = b - a;
                    // just in front of the walls, which share its order: above the floor, below plates and doors
                    var sr = WhiteQuad("Wire", (a + b) * 0.5f + new Vector3(0f, 0f, -0.01f), new Vector3(along.magnitude, wireWidth, 1f), 1);
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
                    wires.Add((sr, p, color));
                }
            }
        }

        // Sections behind the player's checkpoints are shaded, and the checkpoint the player starts from is lit like the start.
        void RefreshSections()
        {
            foreach (var shade in closedShades) if (shade) Destroy(shade);
            closedShades.Clear();
            if (layout == null) return;
            var closed = game.Session.World.ClosedSection;
            var color = palette.background;
            color.a = 0.65f;
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width;)
                {
                    if (!Shaded(x, y, closed)) { x++; continue; }
                    int from = x;
                    while (x < layout.Width && Shaded(x, y, closed)) x++;   // one quad per run of shaded cells in a row
                    var shade = WhiteQuad("Closed", new Vector3((from + x) * 0.5f, -(y + 0.5f), 0f), new Vector3(x - from, 1f, 1f), 13);
                    shade.color = color;
                    closedShades.Add(shade.gameObject);
                }
            foreach (var (renderer, checkpoint) in checkpointViews)
                if (renderer) renderer.color = checkpoint == game.Session.CurrentCheckpoint ? palette.startMarker : palette.rewind;
        }

        // A floor is shaded when its section is closed; a wall only when every floor around it is.
        bool Shaded(int x, int y, bool[] closed)
        {
            var c = new Vector2Int(x, y);
            if (!layout.IsWall(c))
            {
                int s = layout.SectionOf(c);
                return s >= 0 && closed[s];
            }
            bool any = false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new Vector2Int(x + dx, y + dy);
                    if (!layout.InBounds(n) || layout.IsWall(n)) continue;
                    int s = layout.SectionOf(n);
                    if (s < 0 || !closed[s]) return false;
                    any = true;
                }
            return any;
        }

        SpriteRenderer WhiteQuad(string name, Vector3 localPosition, Vector3 localScale, int order)
        {
            if (!whiteSprite)
            {
                var tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            var go = new GameObject(name);
            go.transform.SetParent(tileRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.sortingOrder = order;
            var template = prefabs.floor ? prefabs.floor.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (template) sr.sharedMaterial = template.sharedMaterial;
            return sr;
        }

        Material TerrainMaterial(TerrainTileset terrain)
        {
            if (terrain.Material) return terrain.Material;
            var template = prefabs.floor ? prefabs.floor.GetComponentInChildren<SpriteRenderer>(true) : null;
            return template ? template.sharedMaterial : null;
        }

        // Cells outside the map count as open, so the outer walls get a rim, a face along the bottom and rounded corners.
        bool IsWallCell(int x, int y) => x >= 0 && y >= 0 && x < layout.Width && y < layout.Height && layout.IsWall(new Vector2Int(x, y));

        bool IsHoleCell(int x, int y) =>
            x >= 0 && y >= 0 && x < layout.Width && y < layout.Height && holeGround[layout.Index(new Vector2Int(x, y))] == GroundState.Hole;

        bool IsMapEdge(Vector2Int c) => c.x == 0 || c.y == 0 || c.x == layout.Width - 1 || c.y == layout.Height - 1;

        void OnLoopReady(int loopNumber)
        {
            if (layout == null) return;
            int echoes = game.EchoCount;
            while (echoViews.Count < echoes) echoViews.Add(Instantiate(prefabs.echo, pieceRoot));
            for (int i = 0; i < echoViews.Count; i++)
            {
                bool used = i < echoes;
                echoViews[i].gameObject.SetActive(used);
                if (used) echoViews[i].Setup(palette.Echo(i), i + 1, palette);
            }
            foreach (var m in markers) m.gameObject.SetActive(false);
        }

        void Clear()
        {
            DestroyChildren(tileRoot);
            DestroyChildren(pieceRoot);
            DestroyChildren(markerRoot);
            plates.Clear();
            doors.Clear();
            grounds.Clear();
            echoViews.Clear();
            crateViews.Clear();
            markers.Clear();
            trailDots.Clear();
            chevrons.Clear();
            wires.Clear();
            checkpointViews.Clear();
            closedShades.Clear();
            if (floorMap) floorMap.ClearAllTiles();
            if (wallMap) wallMap.ClearAllTiles();
            playerView = null;
            layout = null;
            if (rewindOverlay) rewindOverlay.SetVisible(false);
        }

        static void DestroyChildren(Transform root)
        {
            if (!root) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }

        // ---- Events -----------------------------------------------------------------------------

        void OnTicked(TickReport report)
        {
            foreach (var cell in report.Crumbled)
                PlayBurst(cell, new[] { palette.crack, palette.wallFace, palette.floorB }, crumbleParticles);
            foreach (var cell in report.Filled)
                PlayBurst(cell, new[] { palette.crate, palette.crateDark }, crumbleParticles);
        }

        void OnParadox(ParadoxEvent p)
        {
            var marker = Take(markers, prefabs.paradoxMarker, markerRoot);
            marker.transform.localPosition = CellToLocal(p.Cell);
            marker.Setup(p.AttemptedDir, p.Tick, palette.paradox);
            PlayBurst(p.Cell, new[] { palette.paradox, palette.Echo(p.EchoIndex), palette.player }, paradoxParticles);
            var label = TakeIdle(labels, prefabs.floatingLabel, fxRoot, l => l.IsPlaying);
            if (label) label.Play(CellToLocal(p.Cell) + new Vector3(0f, 0.4f, -0.1f), "PARADOX", palette.paradox);
            shake = shakeAmount;
        }

        void OnSolved(SolveResult result)
        {
            if (layout != null) PlayBurst(game.Session.World.Player.Cell, new[] { palette.exit, palette.player, palette.Channel(1) }, solveParticles);
        }

        void PlayBurst(Vector2Int cell, Color[] colors, int count)
        {
            if (!prefabs.burst) return;
            var burst = TakeIdle(bursts, prefabs.burst, fxRoot, b => b.IsPlaying);
            burst.Play(CellToLocal(cell) + new Vector3(0f, 0f, -0.1f), colors, count);
        }

        // ---- Per frame ----------------------------------------------------------------------------

        void LateUpdate()
        {
            if (layout == null || !game.HasLevel) return;
            var session = game.Session;
            bool rewinding = game.Phase == LoopPhase.Rewinding;

            if (shakeRoot)
            {
                shake = Mathf.MoveTowards(shake, 0f, shakeDecay * shakeAmount * Time.deltaTime);
                shakeRoot.localPosition = shake > 0f ? (Vector3)(UnityEngine.Random.insideUnitCircle * shake) : Vector3.zero;
            }
            if (rewindOverlay) rewindOverlay.SetVisible(rewinding);

            if (rewinding) DrawSnapshot(session);
            else DrawLive(session);

            float viewTick = game.ViewTick;
            // while rewinding past the moment an echo vanished, its marker disappears again
            foreach (var m in markers)
                if (m.gameObject.activeSelf) m.SetShown(!rewinding || viewTick >= m.Tick + 0.5f);

            DrawTrails(session, rewinding || game.Phase == LoopPhase.Solved);
        }

        void DrawLive(LoopSession session)
        {
            var world = session.World;
            float f = game.StepBlend;
            stackCounts.Clear();
            for (int i = 0; i < world.Bodies.Count; i++)
            {
                var b = world.Bodies[i];
                var view = b.Kind == BodyKind.Player ? playerView : (b.EchoIndex < echoViews.Count ? echoViews[b.EchoIndex] : null);
                if (!view) continue;
                DrawBody(view, Vector2.Lerp(b.PrevCell, b.Cell, f), b.IsLiving, b.Facing, b.Cell, b.Kind == BodyKind.Player);
            }
            for (int i = 0; i < world.Crates.Count && i < crateViews.Count; i++)
            {
                var c = world.Crates[i];
                DrawCrate(crateViews[i], Vector2.Lerp(c.PrevCell, c.Cell, f), c.Sunk);
            }
            DrawTiles(world.DoorOpen, world.Ground);
        }

        void DrawSnapshot(LoopSession session)
        {
            var snaps = session.Snapshots;
            float pos = Mathf.Max(0f, game.RewindPosition);
            int i0 = Mathf.Min(Mathf.FloorToInt(pos), snaps.Count - 1), i1 = Mathf.Min(snaps.Count - 1, i0 + 1);
            float f = pos - i0;
            var a = snaps[i0];
            var b = snaps[i1];
            var bodies = session.World.Bodies;
            stackCounts.Clear();
            for (int i = 0; i < bodies.Count && i < a.BodyCells.Length; i++)
            {
                var body = bodies[i];
                var view = body.Kind == BodyKind.Player ? playerView : (body.EchoIndex < echoViews.Count ? echoViews[body.EchoIndex] : null);
                if (!view) continue;
                DrawBody(view, Vector2.Lerp(a.BodyCells[i], b.BodyCells[i], f), a.BodyLiving[i], a.BodyFacing[i], a.BodyCells[i], false);
            }
            for (int i = 0; i < a.CrateCells.Length && i < crateViews.Count; i++)
                DrawCrate(crateViews[i], Vector2.Lerp(a.CrateCells[i], b.CrateCells[i], f), a.CrateSunk[i]);
            DrawTiles(a.DoorOpen, a.Ground);
        }

        void DrawBody(BodyView view, Vector2 cell, bool living, Vector2Int facing, Vector2Int roundedCell, bool recording)
        {
            if (view.gameObject.activeSelf != living) view.gameObject.SetActive(living);
            if (!living) return;
            view.transform.localPosition = CellToLocal(cell) + new Vector3(0f, 0f, recording ? -0.02f : -0.01f);
            view.SetFacing(facing);
            stackCounts.TryGetValue(roundedCell, out int stacked);
            stackCounts[roundedCell] = stacked + 1;
            view.SetStackIndex(stacked);
            view.SetRecording(recording && game.Phase == LoopPhase.Playing);
        }

        void DrawCrate(CrateView view, Vector2 cell, bool sunk)
        {
            if (view.gameObject.activeSelf == sunk) view.gameObject.SetActive(!sunk);
            if (!sunk) view.transform.localPosition = CellToLocal(cell);
        }

        void DrawTiles(bool[] doorOpen, GroundState[] ground)
        {
            // plates are pressed by any living body or crate standing on them in this frame
            for (int i = 0; i < platePressed.Length; i++) platePressed[i] = false;
            foreach (var view in echoViews) MarkPressed(view);
            MarkPressed(playerView);
            foreach (var crate in crateViews) MarkPressed(crate ? crate.gameObject : null);
            for (int i = 0; i < plates.Count && i < platePressed.Length; i++) plates[i].SetPressed(platePressed[i]);
            foreach (var wire in wires)
            {
                var c = wire.color;
                c.a = platePressed[wire.plate] ? wireLitAlpha : wireIdleAlpha;
                wire.renderer.color = c;
            }

            for (int i = 0; i < doors.Count && i < doorOpen.Length; i++)
            {
                var linked = layout.DoorPlates(i);
                var pips = doorPips[i];
                for (int k = 0; k < pips.Length; k++) pips[k] = platePressed[linked[k]];
                doors[i].SetState(doorOpen[i], pips);
            }
            // Holes come and go (floor crumbles, crates fill them, the rewind undoes both), so their tiles are picked
            // every frame from the ground being shown. Only cells that can change have a GroundView, so this stays cheap.
            var terrain = Terrain;
            holeGround = ground;
            foreach (var pair in grounds)
            {
                var state = ground[pair.Key];
                pair.Value.SetState(state);
                if (!terrain || state != GroundState.Hole) continue;
                var cell = layout.CellOf(pair.Key);
                int mask = TerrainTileset.NeighbourMask(isHoleCell ??= IsHoleCell, cell.x, cell.y);
                pair.Value.SetHoleTile(terrain.Tile(TerrainTileset.Kind.Hole, mask, palette));
            }
        }

        void MarkPressed(Component view) => MarkPressed(view ? view.gameObject : null);

        void MarkPressed(GameObject go)
        {
            if (!go || !go.activeSelf) return;
            var p = go.transform.localPosition;
            var cell = new Vector2Int(Mathf.RoundToInt(p.x - 0.5f), Mathf.RoundToInt(-p.y - 0.5f));
            int plate = layout.PlateIndexAt(cell);
            if (plate >= 0) platePressed[plate] = true;
        }

        void DrawTrails(LoopSession session, bool hidden)
        {
            int dots = 0, arrows = 0;
            int preview = game.Config.PreviewTicks;
            if (!hidden && preview > 0)
            {
                foreach (var body in session.World.Bodies)
                {
                    if (body.Kind != BodyKind.Echo || !body.Alive) continue;
                    var recording = session.Echoes[body.EchoIndex];
                    var color = palette.Echo(body.EchoIndex);
                    var cell = body.Cell;
                    for (int k = 0; k < preview; k++)
                    {
                        var ev = recording[session.Tick + k];
                        if (ev.IsWait) continue;
                        cell += ev.Dir;
                        float alpha = 0.8f * (1f - (float)k / preview);
                        var dot = Take(trailDots, prefabs.trailDot, markerRoot, dots++);
                        dot.transform.localPosition = CellToLocal(cell);
                        dot.color = new Color(color.r, color.g, color.b, alpha);
                        if (ev.Kind == StepKind.Push && prefabs.pushChevron)
                        {
                            var chevron = Take(chevrons, prefabs.pushChevron, markerRoot, arrows++);
                            var d = new Vector3(ev.Dir.x, -ev.Dir.y, 0f);
                            chevron.transform.localPosition = CellToLocal(cell) + d * 0.5f;
                            chevron.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                            chevron.color = new Color(color.r, color.g, color.b, alpha);
                        }
                    }
                }
            }
            for (int i = dots; i < trailDots.Count; i++) if (trailDots[i].gameObject.activeSelf) trailDots[i].gameObject.SetActive(false);
            for (int i = arrows; i < chevrons.Count; i++) if (chevrons[i].gameObject.activeSelf) chevrons[i].gameObject.SetActive(false);
        }

        // ---- Pools ----------------------------------------------------------------------------------

        static T Take<T>(List<T> pool, T prefab, Transform parent, int index) where T : Component
        {
            while (pool.Count <= index) pool.Add(Instantiate(prefab, parent));
            var item = pool[index];
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            return item;
        }

        static T Take<T>(List<T> pool, T prefab, Transform parent) where T : Component
        {
            foreach (var item in pool)
                if (!item.gameObject.activeSelf) { item.gameObject.SetActive(true); return item; }
            var made = Instantiate(prefab, parent);
            pool.Add(made);
            return made;
        }

        static T TakeIdle<T>(List<T> pool, T prefab, Transform parent, Func<T, bool> busy) where T : Component
        {
            if (!prefab) return null;
            foreach (var item in pool) if (!busy(item)) return item;
            var made = Instantiate(prefab, parent);
            made.gameObject.SetActive(false);
            pool.Add(made);
            return made;
        }
    }
}
