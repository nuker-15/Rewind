using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>How the camera arrives in a room: sliding over (the clock waits for it) or cutting straight there.</summary>
    public enum RoomTransition : byte { Slide, Cut }

    public readonly struct Room
    {
        /// <summary>The room's cells, its walls included.</summary>
        public readonly RectInt Cells;
        public readonly RoomTransition Transition;
        public Room(RectInt cells, RoomTransition transition) { Cells = cells; Transition = transition; }
        /// <summary>True for cells inside the room or on its walls, so a doorway in a shared wall is in both rooms.</summary>
        public bool Contains(Vector2Int cell) => Cells.Contains(cell);
    }

    /// <summary>
    /// The rooms of a level: rectangles drawn on the map, walls included, that the camera frames one at a time. Rooms
    /// can be any size and may share a wall, but their insides never overlap. Written as "x,y,w,h,s" entries separated
    /// by ';' (s slides to the room, c cuts to it). A level without rooms is one room: the whole map.
    /// </summary>
    public static class LevelRooms
    {
        public const int MinSize = 4;

        /// <summary>The rooms of a map, never empty: without any, the whole map is the one room.</summary>
        public static List<Room> For(string text, int mapWidth, int mapHeight)
        {
            var map = new RectInt(0, 0, mapWidth, mapHeight);
            var rooms = Parse(text).Where(r => Inside(r.Cells, map)).ToList();
            if (rooms.Count == 0) rooms.Add(new Room(map, RoomTransition.Slide));
            return rooms;
        }

        /// <summary>The room holding a cell, keeping <paramref name="current"/> while the cell is still in it; -1 if none.</summary>
        public static int IndexOf(IReadOnlyList<Room> rooms, Vector2Int cell, int current = -1)
        {
            if (current >= 0 && current < rooms.Count && rooms[current].Contains(cell)) return current;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Contains(cell)) return i;
            return -1;
        }

        /// <summary>True if the insides of two rooms overlap. Sharing a wall is fine.</summary>
        public static bool Overlaps(RectInt a, RectInt b) =>
            a.xMin + 1 < b.xMax - 1 && b.xMin + 1 < a.xMax - 1 && a.yMin + 1 < b.yMax - 1 && b.yMin + 1 < a.yMax - 1;

        /// <summary>
        /// Adds a room. Drawn into solid wall, it is carved out too: walls around, floor inside. Returns an error message
        /// instead if the room is too small or its inside overlaps another room's.
        /// </summary>
        public static string TryAdd(char[][] grid, ref string text, RectInt cells)
        {
            if (cells.width < MinSize || cells.height < MinSize) return $"A ROOM IS AT LEAST {MinSize} x {MinSize}, WALLS INCLUDED.";
            var rooms = Parse(text);
            if (rooms.Any(r => Overlaps(r.Cells, cells))) return "ROOMS CAN SHARE A WALL BUT NOT OVERLAP.";
            bool solid = true;
            for (int y = cells.yMin; y < cells.yMax; y++)
                for (int x = cells.xMin; x < cells.xMax; x++)
                    solid &= grid[y][x] == '#';
            if (solid)
                for (int y = cells.yMin; y < cells.yMax; y++)
                    for (int x = cells.xMin; x < cells.xMax; x++)
                        grid[y][x] = x == cells.xMin || y == cells.yMin || x == cells.xMax - 1 || y == cells.yMax - 1 ? '#' : '.';
            rooms.Add(new Room(cells, RoomTransition.Slide));
            text = Format(rooms);
            return null;
        }

        /// <summary>Removes the room holding a cell (its tiles stay). Returns false if there is none.</summary>
        public static bool TryRemove(ref string text, Vector2Int cell)
        {
            var rooms = Parse(text);
            int i = IndexOf(rooms, cell);
            if (i < 0) return false;
            rooms.RemoveAt(i);
            text = Format(rooms);
            return true;
        }

        public static string SetTransition(string text, int index, RoomTransition transition)
        {
            var rooms = Parse(text);
            if (index < 0 || index >= rooms.Count) return text;
            rooms[index] = new Room(rooms[index].Cells, transition);
            return Format(rooms);
        }

        /// <summary>Moves rooms with their corners when the map changes; a room losing a corner is dropped.</summary>
        public static string Move(string text, Func<Vector2Int, Vector2Int> move) =>
            Format(Parse(text).Select(r =>
            {
                Vector2Int a = move(r.Cells.min), b = move(r.Cells.max - Vector2Int.one);
                bool gone = a.x < 0 || a.y < 0 || b.x < 0 || b.y < 0;
                return gone ? (Room?)null : new Room(new RectInt(Vector2Int.Min(a, b), Vector2Int.Max(a, b) - Vector2Int.Min(a, b) + Vector2Int.one), r.Transition);
            }).Where(r => r.HasValue).Select(r => r.Value));

        /// <summary>Drops rooms that no longer fit in the map.</summary>
        public static string Clean(string text, int mapWidth, int mapHeight) =>
            Format(Parse(text).Where(r => Inside(r.Cells, new RectInt(0, 0, mapWidth, mapHeight))));

        /// <summary>Reads the rooms, skipping malformed entries.</summary>
        public static List<Room> Parse(string text)
        {
            var rooms = new List<Room>();
            if (string.IsNullOrWhiteSpace(text)) return rooms;
            foreach (var raw in text.Split(';'))
            {
                var p = raw.Split(',');
                if (p.Length != 5 || !int.TryParse(p[0].Trim(), out int x) || !int.TryParse(p[1].Trim(), out int y)
                    || !int.TryParse(p[2].Trim(), out int w) || !int.TryParse(p[3].Trim(), out int h) || w < 1 || h < 1) continue;
                rooms.Add(new Room(new RectInt(x, y, w, h), p[4].Trim() == "c" ? RoomTransition.Cut : RoomTransition.Slide));
            }
            return rooms;
        }

        public static string Format(IEnumerable<Room> rooms) =>
            string.Join(";", rooms.Select(r => $"{r.Cells.x},{r.Cells.y},{r.Cells.width},{r.Cells.height},{(r.Transition == RoomTransition.Cut ? "c" : "s")}"));

        static bool Inside(RectInt cells, RectInt map) => cells.xMin >= 0 && cells.yMin >= 0 && cells.xMax <= map.xMax && cells.yMax <= map.yMax;
    }
}
