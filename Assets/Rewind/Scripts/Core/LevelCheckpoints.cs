using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>
    /// Settings of the checkpoints (+) in a map, written as "x,y,par,seconds" entries separated by ';'. A checkpoint
    /// without an entry has par 1 and the level's loop length; seconds of 0 also mean the level's loop length.
    /// </summary>
    public static class LevelCheckpoints
    {
        public const char Glyph = '+';

        public readonly struct Entry
        {
            public readonly Vector2Int Cell;
            public readonly int Par;
            public readonly float Seconds;
            public Entry(Vector2Int cell, int par, float seconds) { Cell = cell; Par = Mathf.Max(1, par); Seconds = Mathf.Max(0f, seconds); }
        }

        /// <summary>Reads the entries, skipping any that are malformed.</summary>
        public static List<Entry> Parse(string text)
        {
            var list = new List<Entry>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var raw in text.Split(';'))
            {
                var p = raw.Split(',');
                if (p.Length == 4 && int.TryParse(p[0].Trim(), out int x) && int.TryParse(p[1].Trim(), out int y) && int.TryParse(p[2].Trim(), out int par)
                    && float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                {
                    var cell = new Vector2Int(x, y);
                    list.RemoveAll(e => e.Cell == cell);
                    list.Add(new Entry(cell, par, seconds));
                }
            }
            return list;
        }

        public static string Format(IEnumerable<Entry> entries) =>
            string.Join(";", entries.Select(e => $"{e.Cell.x},{e.Cell.y},{e.Par},{e.Seconds.ToString(CultureInfo.InvariantCulture)}"));

        /// <summary>The settings of the checkpoint at a cell, or the defaults.</summary>
        public static Entry Get(string text, Vector2Int cell)
        {
            foreach (var e in Parse(text)) if (e.Cell == cell) return e;
            return new Entry(cell, 1, 0f);
        }

        public static string Set(string text, Entry entry)
        {
            var list = Parse(text);
            list.RemoveAll(e => e.Cell == entry.Cell);
            list.Add(entry);
            return Format(list);
        }

        /// <summary>Moves every entry, for when rows or columns are added or removed.</summary>
        public static string Move(string text, Func<Vector2Int, Vector2Int> move) =>
            Format(Parse(text).Select(e => new Entry(move(e.Cell), e.Par, e.Seconds)));

        /// <summary>Drops entries whose checkpoint is no longer in the grid.</summary>
        public static string Clean(string text, char[][] grid) =>
            Format(Parse(text).Where(e => e.Cell.y >= 0 && e.Cell.y < grid.Length && e.Cell.x >= 0 && e.Cell.x < grid[e.Cell.y].Length && grid[e.Cell.y][e.Cell.x] == Glyph));
    }
}
