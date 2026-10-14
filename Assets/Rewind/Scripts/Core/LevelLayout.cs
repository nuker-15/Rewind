using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    public readonly struct PlateInfo
    {
        public readonly Vector2Int Cell;
        public readonly int Channel;   // colour group, shared with the doors it is linked to (1 and up)
        public PlateInfo(Vector2Int cell, int channel) { Cell = cell; Channel = channel; }
    }

    public readonly struct DoorInfo
    {
        public readonly Vector2Int Cell;
        public readonly int Channel;
        public readonly bool Inverse;
        public DoorInfo(Vector2Int cell, int channel, bool inverse) { Cell = cell; Channel = channel; Inverse = inverse; }
    }

    public readonly struct CheckpointInfo
    {
        public readonly Vector2Int Cell;
        public readonly int Section;    // the section it opens, or -1 if it cannot be reached
        public readonly int Par;
        public readonly float Seconds;  // loop length in its section; 0 means the level's
        public CheckpointInfo(Vector2Int cell, int section, int par, float seconds) { Cell = cell; Section = section; Par = par; Seconds = seconds; }
    }

    /// <summary>
    /// The static shape of a level, parsed from a text map, its plate-to-door links (see <see cref="LevelLinks"/>) and
    /// its checkpoint settings (see <see cref="LevelCheckpoints"/>).
    /// Legend: # wall, S start, X exit, o crate, ~ cracked floor, _ hole, p plate, d door that opens while every plate
    /// linked to it is held, D door that shuts while they are, + checkpoint. Older maps use 1-3 for plates and a-c / A-C
    /// for doors, linked by number. Any other character is plain floor.
    /// Checkpoints split the level into sections: section 0 holds the start, and each checkpoint opens a new one beyond it.
    /// </summary>
    public sealed class LevelLayout
    {
        readonly Tile[] tiles;
        readonly GroundState[] initialGround;
        readonly int[] doorAt;
        readonly int[] plateAt;
        readonly List<PlateInfo> plates = new List<PlateInfo>();
        readonly List<DoorInfo> doors = new List<DoorInfo>();
        readonly List<List<int>> doorPlates = new List<List<int>>();
        readonly List<Vector2Int> crateStarts = new List<Vector2Int>();
        readonly List<int> crackCells = new List<int>();
        readonly List<int> dynamicCells = new List<int>();
        readonly List<CheckpointInfo> checkpoints = new List<CheckpointInfo>();
        readonly int[] checkpointAt;
        readonly int[] sectionOf;
        readonly List<int> sectionParent = new List<int> { -1 };
        readonly List<int> checkpointsToExit = new List<int>();

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
        public IReadOnlyList<CheckpointInfo> Checkpoints => checkpoints;
        public int SectionCount => sectionParent.Count;
        /// <summary>The checkpoints passed on the way from the start to the exit, in order (indices into <see cref="Checkpoints"/>).</summary>
        public IReadOnlyList<int> CheckpointsToExit => checkpointsToExit;

        LevelLayout(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new Tile[width * height];
            initialGround = new GroundState[width * height];
            doorAt = new int[width * height];
            plateAt = new int[width * height];
            checkpointAt = new int[width * height];
            sectionOf = new int[width * height];
            for (int i = 0; i < doorAt.Length; i++) { doorAt[i] = -1; plateAt[i] = -1; checkpointAt[i] = -1; }
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

        public static LevelLayout Parse(string map, string links = null, string checkpoints = null) => Parse(SplitRows(map), links, checkpoints);

        public static LevelLayout Parse(IReadOnlyList<string> rows, string links = null, string checkpoints = null)
        {
            if (rows == null || rows.Count == 0) throw new FormatException("The level map is empty.");
            int width = rows[0].Length;
            for (int y = 1; y < rows.Count; y++)
                if (rows[y].Length != width)
                    throw new FormatException($"Row {y} is {rows[y].Length} characters wide; every row must be {width} wide.");

            var layout = new LevelLayout(width, rows.Count);
            var plateColours = new List<int>();
            var doorColours = new List<int>();
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
                        case LevelCheckpoints.Glyph:
                        {
                            tile = new Tile(TileKind.Checkpoint);
                            var settings = LevelCheckpoints.Get(checkpoints, cell);
                            layout.checkpointAt[i] = layout.checkpoints.Count;
                            layout.checkpoints.Add(new CheckpointInfo(cell, -1, settings.Par, settings.Seconds));
                            break;
                        }
                        case '~':
                            layout.initialGround[i] = GroundState.Cracked;
                            layout.crackCells.Add(i);
                            layout.dynamicCells.Add(i);
                            break;
                        case '_':
                            layout.initialGround[i] = GroundState.Hole;
                            layout.dynamicCells.Add(i);
                            break;
                        default:
                            if (LevelLinks.IsPlate(ch))
                            {
                                tile = new Tile(TileKind.Plate);
                                layout.plateAt[i] = layout.plates.Count;
                                layout.plates.Add(new PlateInfo(cell, 0));
                                plateColours.Add(LevelLinks.ColourOf(ch));
                            }
                            else if (LevelLinks.IsDoor(ch))
                            {
                                bool inverse = char.IsUpper(ch);
                                tile = new Tile(TileKind.Door, 0, inverse);
                                layout.doorAt[i] = layout.doors.Count;
                                layout.doors.Add(new DoorInfo(cell, 0, inverse));
                                layout.doorPlates.Add(new List<int>());
                                doorColours.Add(LevelLinks.ColourOf(ch));
                            }
                            break;
                    }
                    layout.tiles[i] = tile;
                }
            }
            if (!hasStart) throw new FormatException("The level map has no start tile (S).");
            if (!hasExit) throw new FormatException("The level map has no exit tile (X).");

            // older maps: every plate controls every door of its colour number
            for (int d = 0; d < layout.doors.Count; d++)
                if (doorColours[d] > 0)
                    for (int p = 0; p < layout.plates.Count; p++)
                        if (plateColours[p] == doorColours[d]) layout.doorPlates[d].Add(p);
            foreach (var (plateCell, doorCell) in LevelLinks.Parse(links))
            {
                int p = layout.PlateIndexAt(plateCell), d = layout.DoorIndexAt(doorCell);
                if (p < 0 || d < 0)
                    throw new FormatException($"The link {plateCell.x},{plateCell.y}>{doorCell.x},{doorCell.y} does not join a plate to a door.");
                if (!layout.doorPlates[d].Contains(p)) layout.doorPlates[d].Add(p);
            }
            layout.NumberGroups();
            layout.FindSections();
            return layout;
        }

        static readonly Vector2Int[] Steps = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        /// <summary>
        /// Section 0 is everything reachable from the start without crossing a checkpoint (doors count as open); each
        /// checkpoint touching a section opens a new section on its far side, numbered in the order they are found.
        /// </summary>
        void FindSections()
        {
            for (int i = 0; i < sectionOf.Length; i++) sectionOf[i] = -1;
            Flood(Start, 0);
            for (int s = 0; s < sectionParent.Count; s++)
                for (int c = 0; c < checkpoints.Count; c++)
                {
                    var cp = checkpoints[c];
                    if (cp.Section >= 0 || !Touches(cp.Cell, s)) continue;
                    int section = sectionParent.Count;
                    sectionParent.Add(s);
                    checkpoints[c] = new CheckpointInfo(cp.Cell, section, cp.Par, cp.Seconds);
                    Flood(cp.Cell, section);
                }

            // a checkpoint has to cut the level in two: the way back to where it was found, and something new beyond it
            foreach (var cp in checkpoints)
            {
                if (cp.Section < 0) continue;
                bool beyond = false;
                foreach (var d in Steps)
                {
                    int s = SectionOf(cp.Cell + d);
                    if (s == cp.Section) beyond = true;
                    else if (s >= 0 && s != sectionParent[cp.Section])
                        throw new FormatException($"The checkpoint at {cp.Cell.x},{cp.Cell.y} joins more than two parts of the level.");
                }
                if (!beyond)
                    throw new FormatException($"The checkpoint at {cp.Cell.x},{cp.Cell.y} does not cut the level in two: there is a way around it, or nothing behind it.");
            }

            for (int s = SectionOf(Exit); s > 0; s = sectionParent[s])
                checkpointsToExit.Insert(0, checkpoints.FindIndex(c => c.Section == s));
        }

        void Flood(Vector2Int seed, int section)
        {
            var stack = new Stack<Vector2Int>();
            sectionOf[Index(seed)] = section;
            stack.Push(seed);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var d in Steps)
                {
                    var n = c + d;
                    if (!InBounds(n) || IsWall(n) || TileAt(n).Kind == TileKind.Checkpoint || sectionOf[Index(n)] >= 0) continue;
                    sectionOf[Index(n)] = section;
                    stack.Push(n);
                }
            }
        }

        bool Touches(Vector2Int cell, int section)
        {
            foreach (var d in Steps)
                if (SectionOf(cell + d) == section) return true;
            return false;
        }

        /// <summary>Numbers each group of linked plates and doors (1, 2, ...) in reading order, so linked tiles share a colour.</summary>
        void NumberGroups()
        {
            int plateCount = plates.Count;
            var parent = new int[plateCount + doors.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int n)
            {
                while (parent[n] != n) n = parent[n];
                return n;
            }
            for (int d = 0; d < doors.Count; d++)
                foreach (int p in doorPlates[d]) parent[Find(plateCount + d)] = Find(p);

            var group = new int[parent.Length];
            int next = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                int node = plateAt[i] >= 0 ? plateAt[i] : doorAt[i] >= 0 ? plateCount + doorAt[i] : -1;
                if (node < 0) continue;
                int root = Find(node);
                if (group[root] == 0) group[root] = ++next;
                int g = group[root];
                tiles[i] = new Tile(tiles[i].Kind, g, tiles[i].Inverse);
                if (plateAt[i] >= 0) plates[plateAt[i]] = new PlateInfo(plates[plateAt[i]].Cell, g);
                else doors[doorAt[i]] = new DoorInfo(doors[doorAt[i]].Cell, g, doors[doorAt[i]].Inverse);
            }
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
        /// <summary>Indices into <see cref="Plates"/> of the plates linked to a door. It is held while all of them are pressed.</summary>
        public IReadOnlyList<int> DoorPlates(int door) => doorPlates[door];
        public int CheckpointIndexAt(Vector2Int c) => InBounds(c) ? checkpointAt[Index(c)] : -1;

        /// <summary>Par for the whole level: the first section's, plus each checkpoint's on the way to the exit.</summary>
        public int TotalPar(int firstSectionPar)
        {
            int total = firstSectionPar;
            foreach (int i in checkpointsToExit) total += checkpoints[i].Par;
            return total;
        }
        /// <summary>The section a cell belongs to, or -1 for walls and cells that cannot be reached.</summary>
        public int SectionOf(Vector2Int c) => InBounds(c) ? sectionOf[Index(c)] : -1;

        /// <summary>The cells of a section and the walls around it.</summary>
        public RectInt SectionBounds(int section)
        {
            int x0 = Width, y0 = Height, x1 = -1, y1 = -1;
            for (int i = 0; i < sectionOf.Length; i++)
            {
                if (sectionOf[i] != section) continue;
                var c = CellOf(i);
                x0 = Math.Min(x0, c.x); y0 = Math.Min(y0, c.y);
                x1 = Math.Max(x1, c.x); y1 = Math.Max(y1, c.y);
            }
            if (x1 < 0) return new RectInt(0, 0, Width, Height);
            x0 = Math.Max(0, x0 - 1); y0 = Math.Max(0, y0 - 1);
            x1 = Math.Min(Width - 1, x1 + 1); y1 = Math.Min(Height - 1, y1 + 1);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
    }
}
