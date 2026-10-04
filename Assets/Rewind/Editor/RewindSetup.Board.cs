using Rewind.Gameplay;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    // Board tiles, pieces, effects, the Board prefab and the Game prefab.
    public static partial class RewindSetup
    {
        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static class Prefabs
        {
            public static GameObject Board, Game;
        }

        // sorting orders, back to front
        const int OrderFloor = 0, OrderGround = 1, OrderPlate = 2, OrderDoor = 3, OrderStart = 4, OrderTrail = 5,
            OrderMarker = 6, OrderCrate = 7, OrderEcho = 8, OrderPlayer = 11, OrderFx = 14, OrderOverlay = 20;

        static Material unlitSprite;

        static void BuildBoardPrefabs()
        {
            Progress("Building board prefabs", 0.5f);
            unlitSprite = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            var p = Assets.Palette;

            var set = LoadOrCreate<BoardPrefabSet>($"{DataDir}/BoardPrefabSet.asset");
            Assets.PrefabSet = set;

            set.floor = BuildPrefab<FloorView>("Tile_Floor", go =>
            {
                var sr = AddSprite(go, Art.Square, OrderFloor, p.floorA);
                Set(go.AddComponent<FloorView>(), "sprite", sr);
            });

            set.wall = BuildPrefab<WallView>("Tile_Wall", go =>
            {
                var body = AddSprite(go, Art.Square, OrderFloor, p.wall);
                var top = AddSprite(Child(go, "TopEdge", new Vector3(0f, 0.465f), new Vector3(1f, 0.07f, 1f)), Art.Square, OrderFloor + 1, p.wallTop);
                var face = AddSprite(Child(go, "Face", new Vector3(0f, -0.35f), new Vector3(1f, 0.3f, 1f)), Art.Square, OrderFloor + 1, p.wallFace);
                var view = go.AddComponent<WallView>();
                Set(view, "body", body);
                Set(view, "topEdge", top);
                Set(view, "face", face);
            });

            set.plate = BuildPrefab<PlateView>("Tile_Plate", go =>
            {
                var glow = AddSprite(Child(go, "Glow", Vector3.zero, Vector3.one * 1.4f), Art.Glow, OrderPlate - 1, p.Channel(1));
                var fill = AddSprite(Child(go, "Fill", Vector3.zero, Vector3.one * 0.66f), Art.RoundedSquare, OrderPlate, p.Channel(1));
                var frame = AddSprite(Child(go, "Frame", Vector3.zero, Vector3.one * 0.66f), Art.RoundedOutline, OrderPlate + 1, p.Channel(1));
                var view = go.AddComponent<PlateView>();
                Set(view, "fill", fill);
                Set(view, "frame", frame);
                Set(view, "glow", glow);
            });

            set.door = BuildPrefab<DoorView>("Tile_Door", go =>
            {
                var pivot = Child(go, "Panel", Vector3.zero, Vector3.one);
                var panel = AddSprite(Child(pivot, "Closed", Vector3.zero, Vector3.one), Art.DoorBars, OrderDoor, p.Channel(1));
                var stripes = AddSprite(Child(pivot, "ClosedStriped", Vector3.zero, Vector3.one), Art.DoorStripes, OrderDoor, p.Channel(1));
                var open = AddSprite(Child(pivot, "Open", Vector3.zero, Vector3.one), Art.DoorOpen, OrderDoor, p.Channel(1));
                stripes.enabled = false;
                open.enabled = false;
                var pipRoot = Child(go, "Pips", Vector3.zero, Vector3.one);
                var pips = new Object[3];
                for (int i = 0; i < 3; i++)
                    pips[i] = AddSprite(Child(pipRoot, $"Pip{i + 1}", new Vector3((i - 1) * 0.22f, 0f), Vector3.one * 0.15f), Art.Circle, OrderDoor + 2, p.pipDark);
                var view = go.AddComponent<DoorView>();
                Set(view, "panelPivot", pivot.transform);
                Set(view, "closedPanel", panel);
                Set(view, "closedStripes", stripes);
                Set(view, "openFrame", open);
                SetArray(view, "pips", pips);
            });

            set.exit = BuildPrefab<ExitView>("Tile_Exit", go =>
            {
                var rings = new Object[3];
                float[] scales = { 0.28f, 0.46f, 0.64f };
                for (int i = 0; i < 3; i++)
                    rings[i] = AddSprite(Child(go, $"Ring{i + 1}", Vector3.zero, Vector3.one * scales[i]), Art.Ring, OrderDoor, p.exit);
                var view = go.AddComponent<ExitView>();
                SetArray(view, "rings", rings);
            });

            set.ground = BuildPrefab<GroundView>("Tile_Ground", go =>
            {
                var hole = AddSprite(Child(go, "Hole", Vector3.zero, Vector3.one), Art.Square, OrderGround, p.pit);
                var rim = AddSprite(Child(go, "HoleRim", new Vector3(0f, 0.42f), new Vector3(1f, 0.16f, 1f)), Art.Square, OrderGround + 1, p.wallFace);
                var filled = AddSprite(Child(go, "Filled", Vector3.zero, Vector3.one * 0.92f), Art.Crate, OrderGround, p.crate);
                var cracks = AddSprite(Child(go, "Cracks", Vector3.zero, Vector3.one), Art.Cracks, OrderGround + 1, p.crack);
                var view = go.AddComponent<GroundView>();
                Set(view, "cracks", cracks);
                Set(view, "hole", hole);
                Set(view, "holeRim", rim);
                Set(view, "filled", filled);
            });

            set.startMarker = BuildPrefab<Transform>("Tile_StartMarker", go => AddSprite(go, Art.Brackets, OrderStart, p.startMarker)).gameObject;

            set.player = BuildPrefab<BodyView>("Piece_Player", go => BuildBody(go, isEcho: false));
            set.echo = BuildPrefab<BodyView>("Piece_Echo", go => BuildBody(go, isEcho: true));

            set.crate = BuildPrefab<CrateView>("Piece_Crate", go =>
            {
                var sr = AddSprite(go, Art.Crate, OrderCrate, p.crate);
                Set(go.AddComponent<CrateView>(), "sprite", sr);
            });

            set.trailDot = BuildPrefab<SpriteRenderer>("Fx_TrailDot", go =>
            {
                go.transform.localScale = Vector3.one * 0.13f;
                AddSprite(go, Art.Circle, OrderTrail, Color.white);
            });

            set.pushChevron = BuildPrefab<SpriteRenderer>("Fx_PushChevron", go =>
            {
                go.transform.localScale = Vector3.one * 0.26f;
                AddSprite(go, Art.Triangle, OrderTrail, Color.white);
            });

            set.paradoxMarker = BuildPrefab<ParadoxMarkerView>("Fx_ParadoxMarker", go =>
            {
                var outline = AddSprite(Child(go, "Outline", Vector3.zero, Vector3.one * 0.6f), Art.DashedOutline, OrderMarker, p.paradox);
                var cross = AddSprite(Child(go, "Cross", Vector3.zero, Vector3.one * 0.3f), Art.Cross, OrderMarker, p.paradox);
                var arrow = AddSprite(Child(go, "Arrow", new Vector3(0.62f, 0f), Vector3.one * 0.24f), Art.Triangle, OrderMarker, p.paradox);
                var view = go.AddComponent<ParadoxMarkerView>();
                Set(view, "outline", outline);
                Set(view, "cross", cross);
                Set(view, "arrow", arrow);
            });

            set.burst = BuildPrefab<SpriteBurst>("Fx_Burst", go =>
            {
                var parts = new Object[44];
                for (int i = 0; i < parts.Length; i++)
                {
                    var child = Child(go, $"Bit{i:00}", Vector3.zero, Vector3.one * 0.1f);
                    parts[i] = AddSprite(child, Art.Square, OrderFx, Color.white);
                    child.SetActive(false);
                }
                SetArray(go.AddComponent<SpriteBurst>(), "particles", parts);
            });

            set.floatingLabel = BuildPrefab<FloatingLabel>("Fx_FloatingLabel", go =>
            {
                var text = WorldText(go, "PARADOX", 4.2f, p.paradox, OrderFx + 1);
                Set(go.AddComponent<FloatingLabel>(), "label", text);
            });

            EditorUtility.SetDirty(set);

            // the board itself
            string boardPath = $"{BoardDir}/Board.prefab";
            if (ShouldBuild(boardPath))
            {
                var root = new GameObject("Board");
                var shake = Child(root, "Shake", Vector3.zero, Vector3.one);
                var tiles = Child(shake, "Tiles", Vector3.zero, Vector3.one);
                var pieces = Child(shake, "Pieces", Vector3.zero, Vector3.one);
                var markers = Child(shake, "Markers", Vector3.zero, Vector3.one);
                var fx = Child(shake, "Fx", Vector3.zero, Vector3.one);
                var overlayGo = Child(shake, "RewindOverlay", Vector3.zero, Vector3.one);
                var tint = AddSprite(Child(overlayGo, "Tint", Vector3.zero, Vector3.one), Art.Square, OrderOverlay, p.rewind);
                var icon = Child(overlayGo, "Icon", Vector3.zero, Vector3.one);
                var left = AddSprite(Child(icon, "ArrowA", new Vector3(-0.42f, 0f), Vector3.one), Art.Triangle, OrderOverlay + 1, Color.white);
                var right = AddSprite(Child(icon, "ArrowB", new Vector3(0.42f, 0f), Vector3.one), Art.Triangle, OrderOverlay + 1, Color.white);
                left.transform.localRotation = right.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                var overlay = overlayGo.AddComponent<RewindOverlay>();
                Set(overlay, "tint", tint);
                Set(overlay, "icon", icon.transform);
                SetArray(overlay, "iconParts", new Object[] { left, right });
                overlayGo.SetActive(false);

                var view = root.AddComponent<BoardView>();
                Set(view, "palette", p);
                Set(view, "prefabs", set);
                Set(view, "shakeRoot", shake.transform);
                Set(view, "tileRoot", tiles.transform);
                Set(view, "pieceRoot", pieces.transform);
                Set(view, "markerRoot", markers.transform);
                Set(view, "fxRoot", fx.transform);
                Set(view, "rewindOverlay", overlay);
                SavePrefab(root, boardPath);
            }
            Prefabs.Board = AssetDatabase.LoadAssetAtPath<GameObject>(boardPath);
        }

        static void BuildBody(GameObject go, bool isEcho)
        {
            var p = Assets.Palette;
            var color = isEcho ? p.Echo(0) : p.player;
            int baseOrder = isEcho ? OrderEcho : OrderPlayer;
            var view = go.AddComponent<BodyView>();

            if (!isEcho) Set(view, "glow", AddSprite(Child(go, "Glow", Vector3.zero, Vector3.one * 1.3f), Art.Glow, baseOrder - 1, color));
            var fill = AddSprite(Child(go, "Fill", Vector3.zero, Vector3.one * 0.6f), Art.RoundedSquare, baseOrder, color);
            Set(view, "fill", fill);
            if (isEcho)
            {
                Set(view, "outline", AddSprite(Child(go, "Outline", Vector3.zero, Vector3.one * 0.6f), Art.RoundedOutline, baseOrder + 1, color));
                Set(view, "fillAlpha", 0.26f);
            }
            var eyeScale = new Vector3(0.078f, 0.144f, 1f);
            var eyeL = Child(go, "EyeLeft", new Vector3(-0.114f, 0.03f), eyeScale);
            var eyeR = Child(go, "EyeRight", new Vector3(0.114f, 0.03f), eyeScale);
            AddSprite(eyeL, Art.Square, baseOrder + 1, isEcho ? color : p.background);
            AddSprite(eyeR, Art.Square, baseOrder + 1, isEcho ? color : p.background);
            Set(view, "eyeLeft", eyeL.transform);
            Set(view, "eyeRight", eyeR.transform);

            if (isEcho)
            {
                var badge = Child(go, "Badge", new Vector3(0.3f, 0.31f), Vector3.one);
                AddSprite(Child(badge, "Back", Vector3.zero, Vector3.one * 0.3f), Art.Circle, baseOrder + 1, p.background);
                var label = WorldText(Child(badge, "Number", new Vector3(0f, -0.01f), Vector3.one), "1", 2.4f, color, baseOrder + 2);
                Set(view, "badge", badge.transform);
                Set(view, "badgeLabel", label);
            }
            else
            {
                var dot = AddSprite(Child(go, "RecordDot", new Vector3(0.3f, 0.3f), Vector3.one * 0.15f), Art.Circle, baseOrder + 2, p.record);
                dot.enabled = false;
                Set(view, "recordDot", dot);
            }
        }

        static void BuildGamePrefab()
        {
            Progress("Building game prefab", 0.6f);
            string path = $"{GamePrefabDir}/Game.prefab";
            if (ShouldBuild(path))
            {
                var root = new GameObject("Game");
                var game = root.AddComponent<RewindGame>();
                Set(game, "config", Assets.Config);
                Set(game, "catalog", Assets.Catalog);

                var input = root.AddComponent<GameplayInput>();
                Set(input, "game", game);
                Set(input, "actions", Assets.Controls);

                var effectsGo = Child(root, "Effects", Vector3.zero, Vector3.one);
                var effects = effectsGo.AddComponent<AudioSource>();
                effects.playOnAwake = false;
                var sweepGo = Child(root, "RewindSweep", Vector3.zero, Vector3.one);
                var sweep = sweepGo.AddComponent<AudioSource>();
                sweep.playOnAwake = false;
                var sfx = root.AddComponent<SfxPlayer>();
                Set(sfx, "game", game);
                Set(sfx, "library", Assets.SfxLibrary);
                Set(sfx, "effects", effects);
                Set(sfx, "rewindSource", sweep);
                SavePrefab(root, path);
            }
            Prefabs.Game = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---- Helpers ------------------------------------------------------------------------------------

        static T BuildPrefab<T>(string name, System.Action<GameObject> build) where T : Component
        {
            string path = $"{BoardDir}/{name}.prefab";
            if (ShouldBuild(path))
            {
                var go = new GameObject(name);
                build(go);
                SavePrefab(go, path);
            }
            var prefab = LoadPrefab<T>(path);
            if (!prefab) Debug.LogError($"Prefab {path} is missing a {typeof(T).Name}.");
            return prefab;
        }

        static GameObject Child(GameObject parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            return go;
        }

        static SpriteRenderer AddSprite(GameObject go, Sprite sprite, int order, Color color)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            if (unlitSprite) sr.sharedMaterial = unlitSprite;
            return sr;
        }

        static TextMeshPro WorldText(GameObject go, string text, float size, Color color, int order)
        {
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
            var renderer = tmp.GetComponent<Renderer>();
            if (renderer) renderer.sortingOrder = order;
            return tmp;
        }
    }
}
