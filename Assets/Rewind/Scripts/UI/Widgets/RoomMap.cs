using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>
    /// A small map of the level's rooms for the HUD: each room drawn where it is, the player's room lit, a dot for the
    /// player and every echo, and a red flash on a room where an echo breaks. Hidden on levels that are one room. Built in code.
    /// </summary>
    public sealed class RoomMap : MonoBehaviour
    {
        const float MaxWidth = 300f, MaxHeight = 84f, Inset = 1.5f, DotSize = 7f, FlashSeconds = 1.2f;

        RewindGame game;
        BoardPalette palette;
        RectTransform boxLayer, dotLayer;
        List<Room> rooms = new List<Room>();
        Vector2Int origin;               // the top-left cell of all the rooms together
        float scale;                     // map pixels per cell
        int current;
        readonly List<Image> boxes = new List<Image>();
        readonly List<Image> dots = new List<Image>();
        float[] flashUntil = new float[0];

        public static RoomMap Build(RectTransform parent, RewindGame game, BoardPalette palette)
        {
            var root = RuntimeUi.Place(RuntimeUi.Rect("RoomMap", parent), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -20f), new Vector2(MaxWidth, MaxHeight));
            var map = root.gameObject.AddComponent<RoomMap>();
            map.game = game;
            map.palette = palette;
            map.boxLayer = RuntimeUi.Stretch(RuntimeUi.Rect("Rooms", root));
            map.dotLayer = RuntimeUi.Stretch(RuntimeUi.Rect("Bodies", root));   // made second, so dots draw over the boxes
            game.LevelLoaded += map.OnLevelLoaded;
            game.ParadoxOccurred += map.OnParadox;
            if (game.HasLevel) map.OnLevelLoaded(game.CurrentLevel, game.LevelIndex);
            return map;
        }

        void OnDestroy()
        {
            if (!game) return;
            game.LevelLoaded -= OnLevelLoaded;
            game.ParadoxOccurred -= OnParadox;
        }

        void OnLevelLoaded(LevelData level, int index)
        {
            rooms = level.RoomsFor(game.Session.Layout);
            foreach (var b in boxes) Destroy(b.gameObject);
            boxes.Clear();
            flashUntil = new float[rooms.Count];
            current = 0;
            gameObject.SetActive(rooms.Count > 1);
            if (rooms.Count <= 1) return;

            // every room at its place, scaled so all of them fit
            var all = rooms[0].Cells;
            foreach (var r in rooms)
            {
                all.min = Vector2Int.Min(all.min, r.Cells.min);
                all.max = Vector2Int.Max(all.max, r.Cells.max);
            }
            origin = all.min;
            scale = Mathf.Min(MaxWidth / all.width, MaxHeight / all.height);
            foreach (var r in rooms)
            {
                var rt = RuntimeUi.Place(RuntimeUi.Rect("Room", boxLayer), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    ToMap(r.Cells.min) + new Vector2(Inset, -Inset), (Vector2)r.Cells.size * scale - new Vector2(Inset, Inset) * 2f);
                boxes.Add(RuntimeUi.Fill(rt, RuntimeUi.Deck, false));
            }
        }

        Vector2 ToMap(Vector2 cell) => new Vector2((cell.x - origin.x) * scale, -(cell.y - origin.y) * scale);

        void OnParadox(ParadoxEvent p)
        {
            int i = LevelRooms.IndexOf(rooms, p.Cell);
            if (i >= 0 && i < flashUntil.Length) flashUntil[i] = Time.unscaledTime + FlashSeconds;
        }

        void LateUpdate()
        {
            if (!game || !game.HasLevel || boxes.Count == 0) return;
            var world = game.Session.World;
            current = Mathf.Max(0, LevelRooms.IndexOf(rooms, world.Player.Cell, current));
            for (int i = 0; i < boxes.Count; i++)
            {
                bool flash = Time.unscaledTime < flashUntil[i] && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
                boxes[i].color = flash ? palette.paradox : i == current ? RuntimeUi.Line : RuntimeUi.WithAlpha(RuntimeUi.Deck, 0.75f);
            }

            int used = 0;
            foreach (var body in world.Bodies)
            {
                if (!body.IsLiving) continue;
                var dot = Dot(used++);
                dot.rectTransform.anchoredPosition = ToMap(body.Cell + new Vector2(0.5f, 0.5f));
                dot.color = body.Kind == BodyKind.Player ? palette.player : palette.Echo(body.EchoIndex);
            }
            for (int i = used; i < dots.Count; i++) dots[i].enabled = false;
        }

        Image Dot(int i)
        {
            while (dots.Count <= i)
            {
                var rt = RuntimeUi.Place(RuntimeUi.Rect("Dot", dotLayer), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DotSize, DotSize));
                dots.Add(RuntimeUi.Fill(rt, Color.white, false));
            }
            dots[i].enabled = true;
            return dots[i];
        }
    }
}
