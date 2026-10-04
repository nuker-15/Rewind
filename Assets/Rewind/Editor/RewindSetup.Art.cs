using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    // Procedural placeholder art. Shapes are white or grayscale so the palette can tint them; replace any PNG in
    // Assets/Rewind/Art/Sprites with real art and the prefabs pick it up.
    public static partial class RewindSetup
    {
        static class Art
        {
            public static Sprite Square, RoundedSquare, RoundedOutline, DashedOutline, Circle, Ring, Glow, Cracks, Crate,
                DoorBars, DoorStripes, DoorOpen, Brackets, Triangle, Cross, Reel, UIPanel, UIOutline, Scanlines, Vignette,
                IconPause, IconRewind, IconStop, IconSound, IconMute, IconFullscreen, IconWindowed;
        }

        static void BuildArt()
        {
            Progress("Drawing sprites", 0.05f);
            Art.Square = MakeSprite("Square", 8, 8, (x, y) => Color.white, 8, filter: FilterMode.Point);
            Art.RoundedSquare = MakeSprite("RoundedSquare", 128, 128, (x, y) => White(Cov(SdBox(x, y, 64, 64, 62, 62, 26))), 128);
            Art.RoundedOutline = MakeSprite("RoundedOutline", 128, 128, (x, y) => White(Cov(Mathf.Abs(SdBox(x, y, 64, 64, 57.5f, 57.5f, 22)) - 4.5f)), 128);
            Art.DashedOutline = MakeSprite("DashedOutline", 128, 128, (x, y) =>
            {
                float ring = Mathf.Abs(SdBox(x, y, 64, 64, 57.5f, 57.5f, 22)) - 4f;
                return White(Cov(ring) * Dash(x, y, 64, 64, 22f, 13f));
            }, 128);
            Art.Circle = MakeSprite("Circle", 64, 64, (x, y) => White(Cov(Len(x - 32, y - 32) - 30.5f)), 64);
            Art.Ring = MakeSprite("Ring", 128, 128, (x, y) =>
            {
                float dx = x - 64, dy = y - 64;
                float a = Mathf.Atan2(dy, dx);
                if (a < 0) a += Mathf.PI * 2f;
                return White(a < Mathf.PI * 1.35f ? Cov(Mathf.Abs(Len(dx, dy) - 57f) - 6f) : 0f);
            }, 128);
            Art.Glow = MakeSprite("Glow", 64, 64, (x, y) =>
            {
                float a = Mathf.Clamp01(1f - Len(x - 32, y - 32) / 32f);
                return White(a * a);
            }, 64);
            Art.Cracks = MakeSprite("Cracks", 128, 128, (x, y) => White(Cov(CrackDistance(x, y, 128) - 2.4f)), 128);
            Art.Crate = MakeSprite("Crate", 128, 128, CratePixel, 128);
            Art.DoorBars = MakeSprite("DoorBars", 128, 128, (x, y) =>
            {
                float body = SdBox(x, y, 64, 64, 59, 59, 8);
                bool bar = y > 12.8f && y < 115.2f && (Mathf.Abs(x - 32) < 4.5f || Mathf.Abs(x - 64) < 4.5f || Mathf.Abs(x - 96) < 4.5f);
                return Gray(bar ? 0.5f : 1f, Cov(body));
            }, 128);
            Art.DoorStripes = MakeSprite("DoorStripes", 128, 128, (x, y) =>
            {
                float body = SdBox(x, y, 64, 64, 59, 59, 8);
                bool stripe = Mod(x + y, 38f) < 14f;
                return Gray(stripe ? 0.45f : 1f, Cov(body));
            }, 128);
            Art.DoorOpen = MakeSprite("DoorOpen", 128, 128, (x, y) =>
            {
                if (y < 11.5f || y > 116.5f) return Color.white;
                float ring = Mathf.Abs(SdBox(x, y, 64, 64, 43.5f, 43.5f, 0)) - 2.2f;
                return White(Cov(ring) * Dash(x, y, 64, 64, 20f, 11f) * 0.55f);
            }, 128);
            Art.Brackets = MakeSprite("Brackets", 128, 128, (x, y) =>
            {
                const float e = 12.8f, k = 20.5f;
                float d = float.MaxValue;
                foreach (var (cx, cy, sx, sy) in new[] { (e, e, 1f, 1f), (128 - e, e, -1f, 1f), (e, 128 - e, 1f, -1f), (128 - e, 128 - e, -1f, -1f) })
                {
                    d = Mathf.Min(d, SdSeg(x, y, cx, cy, cx + sx * k, cy));
                    d = Mathf.Min(d, SdSeg(x, y, cx, cy, cx, cy + sy * k));
                }
                return White(Cov(d - 2f));
            }, 128);
            Art.Triangle = MakeSprite("Triangle", 64, 64, (x, y) => White(Cov(SdTriangle(x, y, 10, 6, 58, 32, 10, 58))), 64);
            Art.Cross = MakeSprite("Cross", 64, 64, (x, y) => White(Cov(Mathf.Min(SdSeg(x, y, 12, 12, 52, 52), SdSeg(x, y, 12, 52, 52, 12)) - 4f)), 64);
            Art.Reel = MakeSprite("Reel", 64, 64, (x, y) =>
            {
                float a = Cov(Len(x - 32, y - 32) - 29f);
                float hole = Cov(Len(x - 32, y - 32) - 4f);
                for (int i = 0; i < 3; i++)
                {
                    float ang = (90f + i * 120f) * Mathf.Deg2Rad;
                    hole = Mathf.Max(hole, Cov(Len(x - 32 - Mathf.Cos(ang) * 16f, y - 32 - Mathf.Sin(ang) * 16f) - 6.5f));
                }
                return White(a * (1f - hole));
            }, 64);
            Art.UIPanel = MakeSprite("UIPanel", 64, 64, (x, y) => White(Cov(SdBox(x, y, 32, 32, 31.5f, 31.5f, 16))), 100, new Vector4(20, 20, 20, 20));
            Art.UIOutline = MakeSprite("UIOutline", 64, 64, (x, y) => White(Cov(Mathf.Abs(SdBox(x, y, 32, 32, 30.5f, 30.5f, 15)) - 1.4f)), 100, new Vector4(20, 20, 20, 20));
            Art.Scanlines = MakeSprite("Scanlines", 4, 4, (x, y) => y < 1f ? new Color(0, 0, 0, 1f) : new Color(0, 0, 0, 0), 100, filter: FilterMode.Point);
            Art.Vignette = MakeSprite("Vignette", 256, 256, (x, y) =>
            {
                float r = Len(x - 128, y - 128) / (128f * 1.4142f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, r));
                return new Color(0, 0, 0, a);
            }, 100);

            Art.IconPause = MakeSprite("IconPause", 64, 64, (x, y) =>
                White(Cov(Mathf.Min(SdBox(x, y, 23, 32, 6, 18, 2), SdBox(x, y, 41, 32, 6, 18, 2)))), 64);
            Art.IconRewind = MakeSprite("IconRewind", 64, 64, (x, y) =>
                White(Cov(Mathf.Min(SdTriangle(x, y, 6, 32, 30, 12, 30, 52), SdTriangle(x, y, 30, 32, 54, 12, 54, 52)))), 64);
            Art.IconStop = MakeSprite("IconStop", 64, 64, (x, y) => White(Cov(SdBox(x, y, 32, 32, 17, 17, 3))), 64);
            Art.IconSound = MakeSprite("IconSound", 64, 64, (x, y) =>
            {
                float d = Speaker(x, y);
                float dx = x - 34, dy = y - 32, ang = Mathf.Abs(Mathf.Atan2(dy, dx));
                if (dx > 0 && ang < 0.9f)
                {
                    float r = Len(dx, dy);
                    d = Mathf.Min(d, Mathf.Min(Mathf.Abs(r - 11f), Mathf.Abs(r - 20f)) - 2.2f);
                }
                return White(Cov(d));
            }, 64);
            Art.IconMute = MakeSprite("IconMute", 64, 64, (x, y) =>
            {
                float d = Speaker(x, y);
                d = Mathf.Min(d, Mathf.Min(SdSeg(x, y, 42, 24, 56, 40), SdSeg(x, y, 42, 40, 56, 24)) - 2.5f);
                return White(Cov(d));
            }, 64);
            Art.IconFullscreen = MakeSprite("IconFullscreen", 64, 64, (x, y) => White(Cov(Corners(x, y, 10f, 14f, inward: true) - 2.6f)), 64);
            Art.IconWindowed = MakeSprite("IconWindowed", 64, 64, (x, y) => White(Cov(Corners(x, y, 24f, 12f, inward: false) - 2.6f)), 64);
        }

        // ---- Shapes -----------------------------------------------------------------------------------

        static Color CratePixel(float x, float y)
        {
            float body = SdBox(x, y, 64, 64, 51.2f, 51.2f, 9);
            if (body > 1f) return new Color(0, 0, 0, 0);
            float g = 1f;
            if (body > -8f) g = 0.42f;                                              // rim
            else if (Mathf.Abs(SdBox(x, y, 64, 64, 32.8f, 32.8f, 0)) < 3f) g = 0.42f; // inner frame
            else if (SdSeg(x, y, 31.2f, 31.2f, 96.8f, 96.8f) < 3f) g = 0.42f;       // diagonal plank
            return Gray(g, Cov(body));
        }

        static float CrackDistance(float x, float y, float size)
        {
            // drawn in top-down unit coordinates, like the browser prototype
            var paths = new[]
            {
                new[] { (0.06f, 0.3f), (0.32f, 0.42f), (0.5f, 0.36f), (0.7f, 0.55f), (0.94f, 0.62f) },
                new[] { (0.32f, 0.42f), (0.28f, 0.7f), (0.42f, 0.94f) },
                new[] { (0.5f, 0.36f), (0.6f, 0.06f) },
                new[] { (0.7f, 0.55f), (0.66f, 0.8f) },
            };
            float d = float.MaxValue;
            foreach (var path in paths)
                for (int i = 0; i < path.Length - 1; i++)
                    d = Mathf.Min(d, SdSeg(x, y, path[i].Item1 * size, (1f - path[i].Item2) * size, path[i + 1].Item1 * size, (1f - path[i + 1].Item2) * size));
            return d;
        }

        static float Speaker(float x, float y) =>
            Mathf.Min(SdBox(x, y, 15, 32, 6, 8, 1), SdQuad(x, y, 19, 24, 31, 12, 31, 52, 19, 40));

        static float Corners(float x, float y, float inset, float arm, bool inward)
        {
            float d = float.MaxValue;
            foreach (var (sx, sy) in new[] { (1f, 1f), (-1f, 1f), (1f, -1f), (-1f, -1f) })
            {
                float cx = sx > 0 ? inset : 64 - inset, cy = sy > 0 ? inset : 64 - inset;
                float ax = inward ? sx : -sx, ay = inward ? sy : -sy;
                d = Mathf.Min(d, SdSeg(x, y, cx, cy, cx + ax * arm, cy));
                d = Mathf.Min(d, SdSeg(x, y, cx, cy, cx, cy + ay * arm));
            }
            return d;
        }

        // ---- Distance helpers (pixel units, negative inside) ---------------------------------------------

        static float Cov(float d) => Mathf.Clamp01(0.5f - d);
        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);
        static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);
        static Color White(float a) => new Color(1f, 1f, 1f, a);
        static Color Gray(float g, float a) => new Color(g, g, g, a);

        static float SdBox(float px, float py, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(px - cx) - hw + r, qy = Mathf.Abs(py - cy) - hh + r;
            return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float SdSeg(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(1e-5f, bax * bax + bay * bay));
            return Len(pax - bax * h, pay - bay * h);
        }

        // Signed distance to a convex polygon given in any winding order.
        static float SdConvex(float px, float py, params float[] pts)
        {
            int n = pts.Length / 2;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += pts[i * 2] * pts[j * 2 + 1] - pts[j * 2] * pts[i * 2 + 1];
            }
            float sign = area > 0 ? 1f : -1f;
            float d = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float ex = pts[j * 2] - pts[i * 2], ey = pts[j * 2 + 1] - pts[i * 2 + 1];
                float len = Mathf.Max(1e-5f, Len(ex, ey));
                // outward normal for a counter-clockwise polygon is (ey, -ex)
                float nx = ey / len * sign, ny = -ex / len * sign;
                d = Mathf.Max(d, (px - pts[i * 2]) * nx + (py - pts[i * 2 + 1]) * ny);
            }
            return d;
        }

        static float SdTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy) =>
            SdConvex(px, py, ax, ay, bx, by, cx, cy);

        static float SdQuad(float px, float py, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy) =>
            SdConvex(px, py, ax, ay, bx, by, cx, cy, dx, dy);

        // 1 on dashes, 0 in gaps, following the outline of a box centred at (cx, cy).
        static float Dash(float x, float y, float cx, float cy, float period, float on)
        {
            float t = Mathf.Abs(x - cx) > Mathf.Abs(y - cy) ? y : x;
            return Mod(t + on * 0.5f, period) < on ? 1f : 0f;
        }

        // ---- Texture writing ------------------------------------------------------------------------------

        static Sprite MakeSprite(string name, int width, int height, Func<float, float, Color> pixel, float pixelsPerUnit,
            Vector4 border = default, FilterMode filter = FilterMode.Bilinear)
        {
            string path = $"{ArtDir}/{name}.png";
            if (ShouldBuild(path))
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var px = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        px[y * width + x] = pixel(x + 0.5f, y + 0.5f);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = filter;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) Debug.LogError($"Could not load sprite at {path}.");
            return sprite;
        }
    }
}
