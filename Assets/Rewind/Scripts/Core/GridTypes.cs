using UnityEngine;

namespace Rewind.Core
{
    public enum TileKind : byte { Floor, Wall, Plate, Door, Exit }

    /// <summary>What the floor of a cell is doing. Only cracked floor and holes ever change.</summary>
    public enum GroundState : byte { Solid, Cracked, Stepped, Hole, Filled }

    public enum StepKind : byte { Wait, Move, Push }

    public readonly struct Tile
    {
        public readonly TileKind Kind;
        public readonly int Channel;   // plates and doors: which colour they belong to (1-3)
        public readonly bool Inverse;  // doors: shut while their plates are held

        public Tile(TileKind kind, int channel = 0, bool inverse = false)
        {
            Kind = kind;
            Channel = channel;
            Inverse = inverse;
        }

        public static readonly Tile Wall = new Tile(TileKind.Wall);
        public static readonly Tile Floor = new Tile(TileKind.Floor);
    }

    /// <summary>Grid directions. The grid's y axis points down, matching the way level maps are written.</summary>
    public static class Dir
    {
        public static readonly Vector2Int Up = new Vector2Int(0, -1);
        public static readonly Vector2Int Down = new Vector2Int(0, 1);
        public static readonly Vector2Int Left = new Vector2Int(-1, 0);
        public static readonly Vector2Int Right = new Vector2Int(1, 0);

        /// <summary>Script letters used by recorded solutions and tests: U, D, L, R move, anything else waits.</summary>
        public static Vector2Int? FromScriptChar(char c)
        {
            switch (c)
            {
                case 'U': return Up;
                case 'D': return Down;
                case 'L': return Left;
                case 'R': return Right;
                default: return null;
            }
        }

        public static char ToScriptChar(Vector2Int? dir)
        {
            if (!dir.HasValue) return '.';
            var d = dir.Value;
            if (d == Up) return 'U';
            if (d == Down) return 'D';
            if (d == Left) return 'L';
            if (d == Right) return 'R';
            return '.';
        }
    }
}
