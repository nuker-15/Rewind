using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>
    /// Which plates control which doors, written as "x,y>x,y" pairs (a plate, then a door it controls) separated by ';'.
    /// A door opens (d) or shuts (D) while every plate linked to it is held. Older maps link by colour instead:
    /// plates 1-3 control doors a-c and A-C of the same number.
    /// </summary>
    public static class LevelLinks
    {
        public static bool IsPlate(char c) => c == 'p' || c >= '1' && c <= '3';
        public static bool IsDoor(char c) => c == 'd' || c == 'D' || c >= 'a' && c <= 'c' || c >= 'A' && c <= 'C';

        /// <summary>The colour number (1-3) of a plate or door from an older map, or 0 for anything else.</summary>
        public static int ColourOf(char c) =>
            c >= '1' && c <= '3' ? c - '0' : c >= 'a' && c <= 'c' ? c - 'a' + 1 : c >= 'A' && c <= 'C' ? c - 'A' + 1 : 0;

        /// <summary>Reads a link list. Throws a FormatException for an entry that is not "x,y>x,y".</summary>
        public static List<(Vector2Int plate, Vector2Int door)> Parse(string links) => Read(links, true);

        public static string Format(IEnumerable<(Vector2Int plate, Vector2Int door)> links) =>
            string.Join(";", links.Select(l => $"{l.plate.x},{l.plate.y}>{l.door.x},{l.door.y}"));

        /// <summary>Links the plate to the door, or unlinks them if they already are.</summary>
        public static string Toggle(string links, Vector2Int plate, Vector2Int door)
        {
            var list = Read(links, false);
            if (!list.Remove((plate, door))) list.Add((plate, door));
            return Format(list);
        }

        public static bool Contains(string links, Vector2Int plate, Vector2Int door) => Read(links, false).Contains((plate, door));

        /// <summary>Moves both ends of every link, for when rows or columns are added or removed.</summary>
        public static string Move(string links, Func<Vector2Int, Vector2Int> move) =>
            Format(Read(links, false).Select(l => (move(l.plate), move(l.door))));

        /// <summary>Drops links that no longer join a plate to a door in this grid, e.g. after one was painted over.</summary>
        public static string Clean(string links, char[][] grid)
        {
            bool Is(Vector2Int c, Func<char, bool> kind) => c.y >= 0 && c.y < grid.Length && c.x >= 0 && c.x < grid[c.y].Length && kind(grid[c.y][c.x]);
            return Format(Read(links, false).Where(l => Is(l.plate, IsPlate) && Is(l.door, IsDoor)));
        }

        /// <summary>
        /// Rewrites the colour-linked tiles of an older map (1-3, a-c, A-C) in place as p, d and D, and returns the
        /// links with the colour links written out, so an editor only ever deals with one kind.
        /// </summary>
        public static string ConvertColourLinks(char[][] grid, string links)
        {
            var list = Read(links, false);
            var plates = new List<(Vector2Int cell, int colour)>();
            var doors = new List<(Vector2Int cell, int colour)>();
            for (int y = 0; y < grid.Length; y++)
                for (int x = 0; x < grid[y].Length; x++)
                {
                    char c = grid[y][x];
                    int colour = ColourOf(c);
                    if (colour == 0) continue;
                    if (IsPlate(c)) { plates.Add((new Vector2Int(x, y), colour)); grid[y][x] = 'p'; }
                    else { doors.Add((new Vector2Int(x, y), colour)); grid[y][x] = char.IsUpper(c) ? 'D' : 'd'; }
                }
            foreach (var door in doors)
                foreach (var plate in plates)
                    if (plate.colour == door.colour && !list.Contains((plate.cell, door.cell))) list.Add((plate.cell, door.cell));
            return Format(list);
        }

        static List<(Vector2Int plate, Vector2Int door)> Read(string links, bool strict)
        {
            var list = new List<(Vector2Int, Vector2Int)>();
            if (string.IsNullOrWhiteSpace(links)) return list;
            foreach (var raw in links.Split(';'))
            {
                var entry = raw.Trim();
                if (entry.Length == 0) continue;
                var ends = entry.Split('>');
                if (ends.Length == 2 && TryCell(ends[0], out var plate) && TryCell(ends[1], out var door))
                {
                    if (!list.Contains((plate, door))) list.Add((plate, door));
                }
                else if (strict) throw new FormatException($"\"{entry}\" is not a link. A link looks like 3,4>10,4 (a plate, then a door).");
            }
            return list;
        }

        static bool TryCell(string text, out Vector2Int cell)
        {
            cell = default;
            var parts = text.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int x) || !int.TryParse(parts[1].Trim(), out int y)) return false;
            cell = new Vector2Int(x, y);
            return true;
        }
    }
}
