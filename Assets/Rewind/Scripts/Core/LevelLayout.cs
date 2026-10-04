using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    public readonly struct PlateInfo
    {
        public readonly Vector2Int Cell;
        public readonly int Channel;
        public PlateInfo(Vector2Int cell, int channel) { Cell = cell; Channel = channel; }
    }

    public readonly struct DoorInfo
    {
        public readonly Vector2Int Cell;
        public readonly int Channel;
        public readonly bool Inverse;
        public DoorInfo(Vector2Int cell, int channel, bool inverse) { Cell = cell; Channel = channel; Inverse = inverse; }
    }

    /// <summary>
    /// The static shape of a level, parsed from a text map.
    /// Legend: # wall, S start, X exit, o crate, 1-3 plates, ~ cracked floor, _ hole,
    /// a-c doors (open while every plate of their channel is held), A-C doors (shut while they are).
    /// Any other character is plain floor.
    /// </summary>
    public sealed class LevelLayout
    {
        public const int MaxChannels = 3;

        readonly Tile[] tiles;
        readonly GroundState[] initialGround;
        readonly int[] doorAt;
        readonly int[] plateAt;
        readonly List<PlateInfo> plates = new List<PlateInfo>();
        readonly List<DoorInfo> doors = new List<DoorInfo>();
        readonly List<Vector2Int> crateStarts = new List<Vector2Int>();
        readonly List<int> crackCells = new List<int>();
        readonly List<int> dynamicCells = new List<int>();
        readonly int[] channelTotals = new int[MaxChannels + 1];
        readonly List<int>[] platesByChannel = new List<int>[MaxChannels + 1];

        public int Width { get; }
        public int Height { get; }
        public Vector2Int Start { get; private set; }
        public Vector2Int Exit { get; private set; }
        public IReadOnlyList<PlateInfo> Plates => plates;
        public IReadOnlyList<DoorInfo> Doors => doors;
        public IReadOnlyList<Vector2Int> CrateStarts => crateStarts;
        /// <summary>Cells that start as cracked floor.</summary>
        public IReadOnlyList<int> CrackCells => crackCells;
        /// <summary>Cells whose ground can change during a loop (cracked floor and holes).</summary>
        public IReadOnlyList<int> DynamicCells => dynamicCells;

        LevelLayout(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new Tile[width * height];
            initialGround = new GroundState[width * height];
            doorAt = new int[width * height];
            plateAt = new int[width * height];
            for (int i = 0; i < doorAt.Length; i++) { doorAt[i] = -1; plateAt[i] = -1; }
            for (int c = 0; c <= MaxChannels; c++) platesByChannel[c] = new List<int>();
        }

        public static IReadOnlyList<string> SplitRows(string map)
        {
            var rows = new List<string>();
            if (string.IsNullOrEmpty(map)) return rows;
            foreach (var raw in map.Split('\n'))
            {
                var line = raw.TrimEnd('\r', ' ', '\t');
                if (line.Length > 0) rows.Add(line);
            }
            return rows;
        }

        public static LevelLayout Parse(string map) => Parse(SplitRows(map));

        public static LevelLayout Parse(IReadOnlyList<string> rows)
        {
            if (rows == null || rows.Count == 0) throw new FormatException("The level map is empty.");
            int width = rows[0].Length;
            for (int y = 1; y < rows.Count; y++)
                if (rows[y].Length != width)
                    throw new FormatException($"Row {y} is {rows[y].Length} characters wide; every row must be {width} wide.");

            var layout = new LevelLayout(width, rows.Count);
            bool hasStart = false, hasExit = false;
            for (int y = 0; y < rows.Count; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char ch = rows[y][x];
                    var cell = new Vector2Int(x, y);
                    int i = layout.Index(cell);
                    var tile = Tile.Floor;
                    switch (ch)
                    {
                        case '#': tile = Tile.Wall; break;
                        case 'S': layout.Start = cell; hasStart = true; break;
                        case 'X': tile = new Tile(TileKind.Exit); layout.Exit = cell; hasExit = true; break;
                        case 'o': layout.crateStarts.Add(cell); break;
                        case '~':
                            layout.initialGround[i] = GroundState.Cracked;
                            layout.crackCells.Add(i);
                            layout.dynamicCells.Add(i);
                            break;
                        case '_':
                            layout.initialGround[i] = GroundState.Hole;
                            layout.dynamicCells.Add(i);
                            break;
                        case '1': case '2': case '3':
                        {
                            int channel = ch - '0';
                            tile = new Tile(TileKind.Plate, channel);
                            layout.plateAt[i] = layout.plates.Count;
                            layout.platesByChannel[channel].Add(layout.plates.Count);
                            layout.channelTotals[channel]++;
                            layout.plates.Add(new PlateInfo(cell, channel));
                            break;
                        }
                        case 'a': case 'b': case 'c':
                        case 'A': case 'B': case 'C':
                        {
                            bool inverse = char.IsUpper(ch);
                            int channel = char.ToLowerInvariant(ch) - 'a' + 1;
                            tile = new Tile(TileKind.Door, channel, inverse);
                            layout.doorAt[i] = layout.doors.Count;
                            layout.doors.Add(new DoorInfo(cell, channel, inverse));
                            break;
                        }
                    }
                    layout.tiles[i] = tile;
                }
            }
            if (!hasStart) throw new FormatException("The level map has no start tile (S).");
            if (!hasExit) throw new FormatException("The level map has no exit tile (X).");
            return layout;
        }

        public int CellCount => tiles.Length;
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        public int Index(Vector2Int c) => c.y * Width + c.x;
        public Vector2Int CellOf(int index) => new Vector2Int(index % Width, index / Width);
        public Tile TileAt(Vector2Int c) => InBounds(c) ? tiles[Index(c)] : Tile.Wall;
        public bool IsWall(Vector2Int c) => TileAt(c).Kind == TileKind.Wall;
        public GroundState InitialGround(int index) => initialGround[index];
        public int DoorIndexAt(Vector2Int c) => InBounds(c) ? doorAt[Index(c)] : -1;
        public int PlateIndexAt(Vector2Int c) => InBounds(c) ? plateAt[Index(c)] : -1;
        public int PlateCount(int channel) => channel >= 0 && channel <= MaxChannels ? channelTotals[channel] : 0;
        public IReadOnlyList<int> PlatesOnChannel(int channel) => platesByChannel[Mathf.Clamp(channel, 0, MaxChannels)];
    }
}
