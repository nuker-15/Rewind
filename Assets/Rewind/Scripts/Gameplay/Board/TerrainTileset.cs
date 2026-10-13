using System;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>
    /// Auto-tiled walls on a dual grid. One tile sits on every corner of the cell grid and is chosen by which of the
    /// four cells around that corner are wall, so 16 tiles cover every wall shape, with rounded outer and inner corners.
    /// Tile index bits: top-left 8, top-right 4, bottom-left 2, bottom-right 1 (index 15 is solid wall, 0 is unused).
    /// Put your own sprite in any slot to override it; empty slots are drawn from the "Generated walls" settings, in the
    /// board palette's colours unless that is switched off. Swap the whole set by pointing the BoardPrefabSet at another
    /// tileset, or clear the BoardPrefabSet's terrain field to go back to the old square walls.
    /// </summary>
    [CreateAssetMenu(menuName = "Rewind/Terrain Tileset", fileName = "TerrainTileset", order = 5)]
    public sealed class TerrainTileset : ScriptableObject
    {
        public const int TileCount = 16;
        public const int TopLeft = 8, TopRight = 4, BottomLeft = 2, BottomRight = 1;

        [Tooltip("Optional hand-made wall tiles, by corner mask (top-left 8, top-right 4, bottom-left 2, bottom-right 1). " +
                 "Each covers one cell's area, centred on a grid corner. Empty slots are generated.")]
        [SerializeField] Sprite[] wallTiles = new Sprite[TileCount];

        [Header("Generated walls")]
        [SerializeField, Range(16, 256)] int pixelsPerTile = 64;
        [SerializeField, Tooltip("Take the colours from the board palette instead of the fields below.")] bool usePaletteColors = true;
        [SerializeField] Color topColor = new Color(0.165f, 0.184f, 0.361f);
        [SerializeField] Color rimColor = new Color(0.267f, 0.294f, 0.549f);
        [SerializeField] Color faceColor = new Color(0.106f, 0.122f, 0.263f);
        [SerializeField] Color lineColor = new Color(0.24f, 0.27f, 0.5f);
        [SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField, Range(0f, 0.5f), Tooltip("Rounding of outer corners, in tiles.")] float cornerRadius = 0.16f;
        [SerializeField, Range(0f, 0.5f), Tooltip("Rounding of inner corners, in tiles.")] float innerCornerRadius = 0.08f;
        [SerializeField, Range(0f, 0.45f), Tooltip("Height of the front face shown where floor lies below a wall.")] float faceHeight = 0.26f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Width of the lit edge along the top and sides.")] float rimWidth = 0.035f;
        [SerializeField, Range(0f, 0.45f), Tooltip("How far inside the edge the inset line runs. 0 hides it.")] float lineInset = 0.12f;
        [SerializeField, Range(0f, 0.1f)] float lineWidth = 0.022f;
        [SerializeField, Range(0f, 0.45f), Tooltip("How far the shadow under a wall reaches onto the floor. 0 hides it.")] float shadowLength = 0.14f;

        [Header("Floor")]
        [SerializeField, Tooltip("Optional floor tiles: white detail, tinted by the palette's checker colours. One is picked per cell. Empty keeps plain squares.")]
        Sprite[] floorTiles = new Sprite[0];

        [Header("Rendering")]
        [SerializeField, Tooltip("Sorting order of the wall tiles: above the floor (0), below plates (2).")] int sortingOrder = 1;
        [SerializeField, Tooltip("Optional. When empty the board uses the floor tile's material.")] Material material;

        struct Tones { public Color Top, Rim, Face, Line, Shadow; }

        [NonSerialized] Sprite[] generated;
        [NonSerialized] BoardPalette generatedFor;
        [NonSerialized] bool stale;

        public int SortingOrder => sortingOrder;
        public Material Material => material;
        public int PixelsPerTile => pixelsPerTile;

        /// <summary>Which corners of grid vertex (vx, vy) touch wall cells. Vertex (0, 0) is the top-left corner of the map.</summary>
        public static int MaskAt(Func<int, int, bool> isWall, int vx, int vy) =>
            (isWall(vx - 1, vy - 1) ? TopLeft : 0) | (isWall(vx, vy - 1) ? TopRight : 0) |
            (isWall(vx - 1, vy) ? BottomLeft : 0) | (isWall(vx, vy) ? BottomRight : 0);

        /// <summary>The tile for a corner mask: the override if there is one, otherwise the generated tile.</summary>
        public Sprite WallTile(int mask, BoardPalette palette)
        {
            if (mask <= 0 || mask >= TileCount) return null;
            if (wallTiles != null && mask < wallTiles.Length && wallTiles[mask]) return wallTiles[mask];
            if (generated == null || stale || generatedFor != palette) Generate(palette);
            return generated[mask];
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

        void OnDisable() => Discard();

        void Discard()
        {
            if (generated != null)
                foreach (var sprite in generated)
                {
                    if (!sprite) continue;
                    var tex = sprite.texture;
                    DestroySafe(sprite);
                    DestroySafe(tex);
                }
            generated = null;
            generatedFor = null;
            stale = false;
        }

        static void DestroySafe(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void Generate(BoardPalette palette)
        {
            Discard();
            generated = new Sprite[TileCount];
            generatedFor = palette;
            for (int mask = 1; mask < TileCount; mask++)
            {
                var tex = BuildTexture(mask, palette, false);
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
                sprite.name = tex.name;
                sprite.hideFlags = HideFlags.DontSave;
                generated[mask] = sprite;
            }
        }

        /// <summary>Draws one wall tile with the generated look. Pass readable to save it as a PNG.</summary>
        public Texture2D BuildTexture(int mask, BoardPalette palette, bool readable)
        {
            var tones = TonesFor(palette);
            int n = pixelsPerTile;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = $"Wall_{mask:00}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[n * n];
            const int ss = 2;   // supersamples per axis, for smooth curves
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color sum = Color.clear;   // premultiplied, so transparent samples do not darken edges
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / n - 0.5f;
                            float v = (y + (sy + 0.5f) / ss) / n - 0.5f;   // texture rows run bottom to top, so v points up
                            var c = Shade(mask, u, v, tones);
                            sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    sum /= ss * ss;
                    pixels[y * n + x] = sum.a > 0.0001f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, !readable);
            return tex;
        }

        Tones TonesFor(BoardPalette p)
        {
            if (usePaletteColors && p)
                return new Tones { Top = p.wall, Rim = p.wallTop, Face = p.wallFace, Line = Color.Lerp(p.wall, p.wallTop, 0.6f), Shadow = shadowColor };
            return new Tones { Top = topColor, Rim = rimColor, Face = faceColor, Line = lineColor, Shadow = shadowColor };
        }

        // ---- Shape ----------------------------------------------------------------------------------------
        // (u, v) runs from -0.5 to 0.5 across the tile with the grid corner at (0, 0) and v pointing up. Each quadrant
        // is a quarter of one of the four cells, and that cell carries on past the tile edge, so the shape only ever
        // changes along the two axes through the centre. Every distance used below stays under half a tile, which
        // keeps it inside these four cells and makes neighbouring tiles line up exactly.

        Color Shade(int mask, float u, float v, in Tones t)
        {
            if (Inside(mask, u, v))
            {
                float below = DistanceDown(mask, u, v);
                if (below < faceHeight)
                {
                    // front face: floor lies just below this part of the wall
                    var face = Color.Lerp(t.Face * 0.72f, t.Face, below / Mathf.Max(0.0001f, faceHeight));
                    if (faceHeight - below < rimWidth) face = Color.Lerp(face, t.Rim, 0.55f);   // the lip where top meets face
                    return Opaque(face);
                }
                float d = EdgeDistance(mask, u, v);
                if (d < rimWidth) return Opaque(t.Rim);
                if (lineInset > 0f && lineWidth > 0f && d > lineInset && d < lineInset + lineWidth) return Opaque(Color.Lerp(t.Top, t.Line, 0.85f));
                return Opaque(t.Top);
            }

            if (shadowLength > 0f && t.Shadow.a > 0f)
            {
                float above = DistanceUp(mask, u, v);
                if (above < shadowLength)
                {
                    float k = 1f - above / shadowLength;
                    var s = t.Shadow;
                    s.a *= k * k;
                    return s;
                }
            }
            return Color.clear;
        }

        static Color Opaque(Color c)
        {
            c.a = 1f;
            return c;
        }

        static bool Has(int mask, bool left, bool top) =>
            (mask & (top ? (left ? TopLeft : TopRight) : (left ? BottomLeft : BottomRight))) != 0;

        bool Inside(int mask, float u, float v)
        {
            bool left = u < 0f, top = v >= 0f;
            float a = Mathf.Abs(u), b = Mathf.Abs(v);
            bool self = Has(mask, left, top), across = Has(mask, !left, top), upDown = Has(mask, left, !top);
            if (self)
            {
                // an outer corner: no wall beside it either way, so round it off
                float r = cornerRadius;
                if (!across && !upDown && a < r && b < r) return (r - a) * (r - a) + (r - b) * (r - b) <= r * r;
                return true;
            }
            // an inner corner: wall on three sides, so fill it in with a fillet
            float ri = innerCornerRadius;
            if (across && upDown && Has(mask, !left, !top) && a < ri && b < ri) return (ri - a) * (ri - a) + (ri - b) * (ri - b) > ri * ri;
            return false;
        }

        /// <summary>Distance from a point inside the wall to the nearest floor.</summary>
        float EdgeDistance(int mask, float u, float v)
        {
            bool left = u < 0f, top = v >= 0f;
            float a = Mathf.Abs(u), b = Mathf.Abs(v);
            bool across = Has(mask, !left, top), upDown = Has(mask, left, !top), diagonal = Has(mask, !left, !top);
            if (!across && !upDown)
            {
                float r = cornerRadius;
                if (a < r && b < r) return r - Mathf.Sqrt((r - a) * (r - a) + (r - b) * (r - b));
                return Mathf.Min(a, b);
            }
            if (!across) return a;
            if (!upDown) return b;
            if (!diagonal)
            {
                float ri = innerCornerRadius;
                return Mathf.Sqrt((a + ri) * (a + ri) + (b + ri) * (b + ri)) - ri;
            }
            return 1f;
        }

        /// <summary>How far down from a wall point the floor starts, up to the face height.</summary>
        float DistanceDown(int mask, float u, float v)
        {
            if (faceHeight <= 0f || Inside(mask, u, v - faceHeight)) return faceHeight;
            float lo = 0f, hi = faceHeight;
            for (int i = 0; i < 8; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Inside(mask, u, v - mid)) lo = mid; else hi = mid;
            }
            return hi;
        }

        /// <summary>How far up from a floor point the nearest wall is, up to the shadow length.</summary>
        float DistanceUp(int mask, float u, float v)
        {
            const int steps = 12;
            float step = shadowLength / steps;
            for (int i = 1; i <= steps; i++)
                if (Inside(mask, u, v + i * step))
                {
                    float lo = (i - 1) * step, hi = i * step;
                    for (int k = 0; k < 5; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Inside(mask, u, v + mid)) hi = mid; else lo = mid;
                    }
                    return hi;
                }
            return shadowLength;
        }
    }
}
