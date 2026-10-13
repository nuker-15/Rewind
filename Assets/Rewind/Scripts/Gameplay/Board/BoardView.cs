using System;
using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using UnityEngine;

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
        BodyView playerView;
        LevelLayout layout;
        bool[] platePressed = Array.Empty<bool>();
        bool[][] doorPips = Array.Empty<bool[]>();
        float shake;

        public Bounds WorldBounds { get; private set; }
        public BoardPalette Palette => palette;
        public static Vector3 CellToLocal(Vector2 cell) => new Vector3(cell.x + 0.5f, -(cell.y + 0.5f), 0f);

        TerrainTileset Terrain => prefabs ? prefabs.terrain : null;

        void OnEnable()
        {
            if (!game) return;
            game.LevelLoaded += OnLevelLoaded;
            game.LevelUnloaded += Clear;
            game.LoopReady += OnLoopReady;
            game.Ticked += OnTicked;
            game.ParadoxOccurred += OnParadox;
            game.Solved += OnSolved;
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
        }

        // ---- Building ------------------------------------------------------------------------

        void OnLevelLoaded(LevelData level, int index)
        {
            Clear();
            layout = game.Session.Layout;
            var terrain = Terrain;
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                    BuildCell(new Vector2Int(x, y), terrain);
            if (terrain) BuildTerrain(terrain);

            if (prefabs.startMarker)
            {
                var start = Instantiate(prefabs.startMarker, tileRoot);
                start.transform.localPosition = CellToLocal(layout.Start);
                if (start.TryGetComponent(out SpriteRenderer sr)) sr.color = palette.startMarker;
            }

            platePressed = new bool[layout.Plates.Count];
            doorPips = new bool[layout.Doors.Count][];
            for (int i = 0; i < layout.Doors.Count; i++)
                doorPips[i] = new bool[layout.PlatesOnChannel(layout.Doors[i].Channel).Count];

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
                    if (!IsMapEdge(cell)) BuildFloor(cell, pos, terrain);
                    return;
                }
                var wall = Instantiate(prefabs.wall, tileRoot);
                wall.transform.localPosition = pos;
                bool openAbove = cell.y > 0 && !layout.IsWall(cell + Dir.Up);
                bool openBelow = cell.y < layout.Height - 1 && !layout.IsWall(cell + Dir.Down);
                wall.Setup(openAbove, openBelow, palette);
                return;
            }

            BuildFloor(cell, pos, terrain);

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
                    door.Setup(palette.Channel(tile.Channel), tile.Inverse, across, layout.PlateCount(tile.Channel), palette);
                    doors.Add(door);     // same order as layout.Doors
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

        void BuildFloor(Vector2Int cell, Vector3 pos, TerrainTileset terrain)
        {
            var floor = Instantiate(prefabs.floor, tileRoot);
            floor.transform.localPosition = pos;
            floor.Setup(cell, palette);
            if (terrain) floor.SetTile(terrain.FloorTile(cell));
        }

        // One tile per grid corner, picked by which of the four cells around it are wall (see TerrainTileset).
        void BuildTerrain(TerrainTileset terrain)
        {
            var root = new GameObject("Terrain").transform;
            root.SetParent(tileRoot, false);
            var material = terrain.Material;
            if (!material && prefabs.floor)
            {
                var template = prefabs.floor.GetComponentInChildren<SpriteRenderer>(true);
                if (template) material = template.sharedMaterial;
            }

            for (int vy = 0; vy <= layout.Height; vy++)
                for (int vx = 0; vx <= layout.Width; vx++)
                {
                    int mask = TerrainTileset.MaskAt(IsWallCell, vx, vy);
                    var sprite = terrain.WallTile(mask, palette);
                    if (!sprite) continue;
                    var go = new GameObject($"Wall {vx},{vy}");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(vx, -vy, 0f);
                    // scale the sprite to exactly one cell, plus a hair of overlap so neighbours never show a seam
                    var size = sprite.bounds.size;
                    if (size.x > 0f && size.y > 0f) go.transform.localScale = new Vector3(1.01f / size.x, 1.01f / size.y, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = terrain.SortingOrder;
                    if (material) sr.sharedMaterial = material;
                }
        }

        // Cells outside the map count as open, so the outer walls get a rim, a face along the bottom and rounded corners.
        bool IsWallCell(int x, int y) => x >= 0 && y >= 0 && x < layout.Width && y < layout.Height && layout.IsWall(new Vector2Int(x, y));

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

            for (int i = 0; i < doors.Count && i < doorOpen.Length; i++)
            {
                var onChannel = layout.PlatesOnChannel(layout.Doors[i].Channel);
                var pips = doorPips[i];
                for (int k = 0; k < pips.Length; k++) pips[k] = platePressed[onChannel[k]];
                doors[i].SetState(doorOpen[i], pips);
            }
            foreach (var pair in grounds) pair.Value.SetState(ground[pair.Key]);
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
