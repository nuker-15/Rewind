using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>
    /// Whole-map (canvas) edits shared by the level editors. New space is solid wall, ready to draw rooms into. Each
    /// edit returns the new grid and where every old cell went (<see cref="Gone"/> if it was cut), so links, checkpoint
    /// settings and rooms can follow their tiles.
    /// </summary>
    public static class MapEdit
    {
        public static readonly Vector2Int Gone = new Vector2Int(-1, -1);

        /// <summary>Resizes the map by adding or removing columns just inside its right edge and rows just inside its bottom edge.</summary>
        public static (char[][] grid, Func<Vector2Int, Vector2Int> move) Resize(char[][] grid, int width, int height)
        {
            int atX = grid[0].Length - 1, atY = grid.Length - 1;   // the right and bottom edges
            int dx = width - grid[0].Length, dy = height - grid.Length;
            var result = new char[height][];
            for (int y = 0; y < height; y++)
            {
                result[y] = new char[width];
                int oy = Old(y, atY, dy);
                for (int x = 0; x < width; x++)
                {
                    int ox = Old(x, atX, dx);
                    result[y][x] = ox >= 0 && oy >= 0 ? grid[oy][ox] : '#';
                }
            }
            return (result, c =>
            {
                int x = New(c.x, atX, dx), y = New(c.y, atY, dy);
                return x < 0 || y < 0 ? Gone : new Vector2Int(x, y);
            });
        }

        /// <summary>Adds solid wall on the sides given, in tiles. Everything moves right by left and down by top.</summary>
        public static (char[][] grid, Func<Vector2Int, Vector2Int> move) Extend(char[][] grid, int left, int top, int right, int bottom)
        {
            int width = grid[0].Length + left + right, height = grid.Length + top + bottom;
            var result = new char[height][];
            for (int y = 0; y < height; y++)
            {
                result[y] = new char[width];
                for (int x = 0; x < width; x++)
                {
                    int ox = x - left, oy = y - top;
                    result[y][x] = oy >= 0 && oy < grid.Length && ox >= 0 && ox < grid[oy].Length ? grid[oy][ox] : '#';
                }
            }
            return (result, c => new Vector2Int(c.x + left, c.y + top));
        }

        /// <summary>Cuts away the solid wall around the level, keeping one layer of wall around everything else and every room.</summary>
        public static (char[][] grid, Func<Vector2Int, Vector2Int> move) Trim(char[][] grid, IEnumerable<RectInt> rooms)
        {
            int w = grid[0].Length, h = grid.Length;
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            void Include(int xa, int ya, int xb, int yb) { x0 = Math.Min(x0, xa); y0 = Math.Min(y0, ya); x1 = Math.Max(x1, xb); y1 = Math.Max(y1, yb); }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (grid[y][x] != '#') Include(x - 1, y - 1, x + 1, y + 1);
            foreach (var r in rooms) Include(r.xMin, r.yMin, r.xMax - 1, r.yMax - 1);
            if (x1 < 0) return (grid, c => c);
            var keep = new RectInt(Math.Max(0, x0), Math.Max(0, y0), 0, 0);
            keep.xMax = Math.Min(w - 1, x1) + 1;
            keep.yMax = Math.Min(h - 1, y1) + 1;
            var result = new char[keep.height][];
            for (int y = 0; y < keep.height; y++)
            {
                result[y] = new char[keep.width];
                Array.Copy(grid[keep.y + y], keep.x, result[y], 0, keep.width);
            }
            return (result, c => keep.Contains(c) ? c - keep.position : Gone);
        }

        // Where a new column (or row) came from when d were added (or removed, if negative) just before index at; -1 if added.
        static int Old(int n, int at, int d) => n < Math.Min(at, at + d) ? n : d > 0 && n < at + d ? -1 : n - d;

        // Where an old column (or row) went; -1 if it was removed.
        static int New(int o, int at, int d) => o >= at ? o + d : d < 0 && o >= at + d ? -1 : o;
    }
}
