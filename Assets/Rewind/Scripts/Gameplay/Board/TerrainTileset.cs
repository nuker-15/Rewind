using System;
using System.Collections.Generic;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>
    /// Auto-tiled walls and holes, as "blob" tile sets. A wall cell (or a hole) picks its tile from which of its eight
    /// neighbours are the same kind, so every edge and every corner is drawn right. A corner neighbour only matters
    /// when both edges beside it match, which leaves 47 different tiles per set.
    /// Neighbour bits: N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128. Slot i of a set holds the tile for Masks[i]
    /// (the inspector shows each slot with its mask). Put your own sprite in any slot to override it; empty slots are
    /// drawn from the settings below, in the board palette's colours unless that is switched off. Swap the whole look
    /// by pointing the BoardPrefabSet at another tileset, or clear that field to go back to the old square walls.
    /// </summary>
    [CreateAssetMenu(menuName = "Rewind/Terrain Tileset", fileName = "TerrainTileset", order = 5)]
    public sealed class TerrainTileset : ScriptableObject
    {
        public enum Kind { Wall, Hole }

        public const int TileCount = 47;
        public const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

        /// <summary>The 47 neighbour masks that look different, in ascending order. Slot i of a tile set is for Masks[i].</summary>
        public static readonly int[] Masks;
        static readonly int[] slotOfMask = new int[256];

        static TerrainTileset()
        {
            var masks = new List<int>();
            for (int m = 0; m < 256; m++) if (Canonical(m) == m) masks.Add(m);
            Masks = masks.ToArray();
            for (int m = 0; m < 256; m++) slotOfMask[m] = Array.IndexOf(Masks, Canonical(m));
        }

        [Tooltip("Optional hand-made wall tiles, one per slot (see the inspector for each slot's neighbours). Empty slots are generated.")]
        [SerializeField] Sprite[] wallTiles = new Sprite[TileCount];
        [Tooltip("Optional hand-made hole tiles, one per slot. Empty slots are generated.")]
        [SerializeField] Sprite[] holeTiles = new Sprite[TileCount];

        [Header("Generated tiles")]
        [SerializeField, Range(16, 256)] int pixelsPerTile = 64;
        [SerializeField, Tooltip("Take the colours from the board palette instead of the fields below.")] bool usePaletteColors = true;

        [Header("Walls")]
        [SerializeField] Color wallTop = new Color(0.165f, 0.184f, 0.361f);
        [SerializeField] Color wallRim = new Color(0.267f, 0.294f, 0.549f);
        [SerializeField] Color wallFace = new Color(0.106f, 0.122f, 0.263f);
        [SerializeField] Color wallLine = new Color(0.24f, 0.27f, 0.5f);
        [SerializeField, Range(0f, 0.5f), Tooltip("Rounding of outer corners, in cells.")] float wallCornerRadius = 0.18f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Height of the front face shown where floor lies below a wall.")] float wallFaceHeight = 0.3f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Width of the lit edge along the top and sides.")] float wallRimWidth = 0.035f;
        [SerializeField, Range(0f, 0.45f), Tooltip("How far inside the edge the inset line runs. 0 hides it.")] float wallLineInset = 0.12f;
        [SerializeField, Range(0f, 0.1f)] float wallLineWidth = 0.022f;

        [Header("Holes")]
        [SerializeField] Color holePit = new Color(0.016f, 0.02f, 0.047f);
        [SerializeField, Tooltip("The far wall of the hole, seen just inside its top edge.")] Color holeWall = new Color(0.106f, 0.122f, 0.263f);
        [SerializeField] Color holeLip = new Color(0.186f, 0.208f, 0.406f);
        [SerializeField, Range(0f, 0.5f)] float holeCornerRadius = 0.2f;
        [SerializeField, Range(0f, 0.5f), Tooltip("How far down the far wall shows before the hole goes dark.")] float holeWallHeight = 0.34f;
        [SerializeField, Range(0f, 0.2f)] float holeLipWidth = 0.045f;

        [Header("Floor")]
        [SerializeField, Tooltip("Optional floor tiles: white detail, tinted by the palette's checker colours. One is picked per cell. Empty keeps plain squares.")]
        Sprite[] floorTiles = new Sprite[0];

        [Header("Rendering")]
        [SerializeField, Tooltip("Sorting order of wall and hole tiles: above the floor (0), below plates (2).")] int sortingOrder = 1;
        [SerializeField, Tooltip("Optional. When empty the board uses the floor tile's material.")] Material material;

        struct Tones { public Color Top, Rim, Face, Line, Pit, PitWall, Lip; }

        [NonSerialized] Sprite[] madeWalls, madeHoles;
        [NonSerialized] BoardPalette madeFor;
        [NonSerialized] bool stale;

        public int SortingOrder => sortingOrder;
        public Material Material => material;
        public int PixelsPerTile => pixelsPerTile;

        /// <summary>Clears corner bits that do not change the look: a corner counts only when both edges beside it are set.</summary>
        public static int Canonical(int mask)
        {
            if ((mask & N) == 0 || (mask & E) == 0) mask &= ~NE;
            if ((mask & S) == 0 || (mask & E) == 0) mask &= ~SE;
            if ((mask & S) == 0 || (mask & W) == 0) mask &= ~SW;
            if ((mask & N) == 0 || (mask & W) == 0) mask &= ~NW;
            return mask;
        }

        public static int SlotOf(int mask) => slotOfMask[mask & 255];

        /// <summary>Which of the eight neighbours of cell (x, y) are the same kind. y grows downwards, as in level maps.</summary>
        public static int NeighbourMask(Func<int, int, bool> same, int x, int y) =>
            (same(x, y - 1) ? N : 0) | (same(x + 1, y - 1) ? NE : 0) | (same(x + 1, y) ? E : 0) | (same(x + 1, y + 1) ? SE : 0) |
            (same(x, y + 1) ? S : 0) | (same(x - 1, y + 1) ? SW : 0) | (same(x - 1, y) ? W : 0) | (same(x - 1, y - 1) ? NW : 0);

        /// <summary>The tile for a neighbour mask: the override in its slot if there is one, otherwise the generated tile.</summary>
        public Sprite Tile(Kind kind, int mask, BoardPalette palette)
        {
            int slot = SlotOf(mask);
            var overrides = kind == Kind.Wall ? wallTiles : holeTiles;
            if (overrides != null && slot < overrides.Length && overrides[slot]) return overrides[slot];
            if (stale || madeWalls == null || madeFor != palette) Discard(palette);
            var made = kind == Kind.Wall ? madeWalls : madeHoles;
            if (!made[slot])
            {
                var tex = BuildTexture(kind, slot, palette, false);
                made[slot] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
                made[slot].name = tex.name;
                made[slot].hideFlags = HideFlags.DontSave;
            }
            return made[slot];
        }

        /// <summary>A floor tile for a cell, or null to keep the plain square. The same cell always gets the same tile.</summary>
        public Sprite FloorTile(Vector2Int cell)
        {
            if (floorTiles == null || floorTiles.Length == 0) return null;
            int hash = (cell.x * 73856093) ^ (cell.y * 19349663);
            return floorTiles[(hash & int.MaxValue) % floorTiles.Length];
        }

        // regenerate on next use after the settings change in the inspector
        void OnValidate() => stale = true;

        void OnDisable() => Discard(null);

        // Tiles are generated one slot at a time, the first time a level needs them.
        void Discard(BoardPalette palette)
        {
            DestroyAll(madeWalls);
            DestroyAll(madeHoles);
            madeWalls = new Sprite[TileCount];
            madeHoles = new Sprite[TileCount];
            madeFor = palette;
            stale = false;
        }

        static void DestroyAll(Sprite[] sprites)
        {
            if (sprites == null) return;
            foreach (var sprite in sprites)
            {
                if (!sprite) continue;
                var tex = sprite.texture;
                DestroySafe(sprite);
                DestroySafe(tex);
            }
        }

        static void DestroySafe(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>Draws one generated tile. Pass readable to save it as a PNG.</summary>
        public Texture2D BuildTexture(Kind kind, int slot, BoardPalette palette, bool readable)
        {
            int mask = Masks[slot];
            var tones = TonesFor(palette);
            int n = pixelsPerTile;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = $"{kind}_{slot:00}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[n * n];
            const int ss = 2;   // supersamples per axis, for smooth curves
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color sum = Color.clear;
                    int covered = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / n - 0.5f;
                            float v = (y + (sy + 0.5f) / ss) / n - 0.5f;   // texture rows run bottom to top, so v points up (north)
                            Color c;
                            bool hit = kind == Kind.Wall ? ShadeWall(mask, u, v, tones, out c) : ShadeHole(mask, u, v, tones, out c);
                            if (!hit) continue;
                            sum += c;
                            covered++;
                        }
                    if (covered == 0) pixels[y * n + x] = new Color32(0, 0, 0, 0);
                    else pixels[y * n + x] = new Color(sum.r / covered, sum.g / covered, sum.b / covered, covered / (float)(ss * ss));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, !readable);
            return tex;
        }

        Tones TonesFor(BoardPalette p)
        {
            if (usePaletteColors && p)
                return new Tones
                {
                    Top = p.wall, Rim = p.wallTop, Face = p.wallFace, Line = Color.Lerp(p.wall, p.wallTop, 0.6f),
                    Pit = p.pit, PitWall = p.wallFace, Lip = Color.Lerp(p.wallFace, p.wallTop, 0.5f),
                };
            return new Tones { Top = wallTop, Rim = wallRim, Face = wallFace, Line = wallLine, Pit = holePit, PitWall = holeWall, Lip = holeLip };
        }

        // ---- Looks ------------------------------------------------------------------------------------------

        bool ShadeWall(int mask, float u, float v, in Tones t, out Color c)
        {
            float r = wallCornerRadius;
            c = Color.clear;
            if (!Inside(mask, u, v, r)) return false;
            float below = Distance(mask, u, v, -1f, wallFaceHeight, r);
            if (below < wallFaceHeight)
            {
                // front face: floor lies just below this part of the wall
                c = Color.Lerp(t.Face * 0.72f, t.Face, below / Mathf.Max(0.0001f, wallFaceHeight));
                if (wallFaceHeight - below < wallRimWidth) c = Color.Lerp(c, t.Rim, 0.55f);   // the lip where top meets face
            }
            else
            {
                float d = EdgeDistance(mask, u, v, r);
                c = d < wallRimWidth ? t.Rim
                    : wallLineInset > 0f && wallLineWidth > 0f && d > wallLineInset && d < wallLineInset + wallLineWidth ? Color.Lerp(t.Top, t.Line, 0.85f)
                    : t.Top;
            }
            c.a = 1f;
            return true;
        }

        bool ShadeHole(int mask, float u, float v, in Tones t, out Color c)
        {
            float r = holeCornerRadius;
            c = Color.clear;
            if (!Inside(mask, u, v, r)) return false;
            // the far wall of the hole shows just inside its top edge, fading into the dark
            float above = Distance(mask, u, v, 1f, holeWallHeight, r);
            c = t.Pit;
            if (above < holeWallHeight)
            {
                float k = above / Mathf.Max(0.0001f, holeWallHeight);
                c = Color.Lerp(t.PitWall, t.Pit, k * k * (3f - 2f * k));
            }
            if (EdgeDistance(mask, u, v, r) < holeLipWidth) c = Color.Lerp(c, t.Lip, 0.85f);
            c.a = 1f;
            return true;
        }

        // ---- Shape ------------------------------------------------------------------------------------------
        // (u, v) runs from -0.5 to 0.5 across the cell, centred on it, with v pointing north. Each quarter of the cell
        // touches one corner, and only the three neighbours around that corner can change it. Every distance used
        // stays under half a cell, so a tile never needs to know anything beyond its eight neighbours.

        static bool Has(int mask, int bit) => (mask & bit) != 0;

        // the side, top-or-bottom and diagonal neighbours around the corner of the cell nearest (u, v)
        static void CornerNeighbours(int mask, float u, float v, out bool side, out bool upDown, out bool diagonal)
        {
            bool east = u >= 0f, north = v >= 0f;
            side = Has(mask, east ? E : W);
            upDown = Has(mask, north ? N : S);
            diagonal = Has(mask, north ? (east ? NE : NW) : (east ? SE : SW));
        }

        /// <summary>Is (u, v) part of the shape? Points past the cell edge belong to the neighbour there.</summary>
        static bool Inside(int mask, float u, float v, float radius)
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            if (au > 0.5f || av > 0.5f)
            {
                int bit = au > 0.5f && av > 0.5f ? (v > 0f ? (u > 0f ? NE : NW) : (u > 0f ? SE : SW))
                    : au > 0.5f ? (u > 0f ? E : W)
                    : (v > 0f ? N : S);
                return Has(mask, bit);
            }
            CornerNeighbours(mask, u, v, out bool side, out bool upDown, out _);
            float a = 0.5f - au, b = 0.5f - av;   // distances to the nearest side edge and top-or-bottom edge
            // an outer corner: nothing of the same kind beside it either way, so round it off
            if (!side && !upDown && a < radius && b < radius) return (radius - a) * (radius - a) + (radius - b) * (radius - b) <= radius * radius;
            return true;
        }

        /// <summary>Distance from a point of the shape to the nearest point outside it.</summary>
        static float EdgeDistance(int mask, float u, float v, float radius)
        {
            CornerNeighbours(mask, u, v, out bool side, out bool upDown, out bool diagonal);
            float a = 0.5f - Mathf.Abs(u), b = 0.5f - Mathf.Abs(v);
            if (!side && !upDown)
            {
                if (a < radius && b < radius) return radius - Mathf.Sqrt((radius - a) * (radius - a) + (radius - b) * (radius - b));
                return Mathf.Min(a, b);
            }
            if (!side) return a;
            if (!upDown) return b;
            if (!diagonal) return Mathf.Sqrt(a * a + b * b);   // an inner corner: only the diagonal neighbour is open
            return 1f;
        }

        /// <summary>How far from (u, v), going straight up (direction 1) or down (-1), the shape ends, up to limit.</summary>
        static float Distance(int mask, float u, float v, float direction, float limit, float radius)
        {
            if (limit <= 0f || Inside(mask, u, v + direction * limit, radius)) return limit;
            float lo = 0f, hi = limit;
            for (int i = 0; i < 8; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Inside(mask, u, v + direction * mid, radius)) lo = mid; else hi = mid;
            }
            return hi;
        }
    }
}
