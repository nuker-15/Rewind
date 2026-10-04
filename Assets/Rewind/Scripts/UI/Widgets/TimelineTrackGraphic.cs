using Rewind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>
    /// Draws one loop as a tape track: a faint bar for its length, a block for every tick it moved, and a cut with a
    /// cross where it vanished.
    /// </summary>
    // Graphic only requires a RectTransform. In the Editor its lazy CanvasRenderer lookup is fooled by Unity's
    // "fake null" from GetComponent, so without this the graphic is silently never rebuilt and draws nothing.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimelineTrackGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] float baseAlpha = 0.2f;
        [SerializeField, Range(0f, 1f)] float moveAlpha = 0.9f;
        [SerializeField] float baseThickness = 6f;
        [SerializeField] float moveThickness = 10f;
        [SerializeField] Color cutColor = new Color(0.07f, 0.08f, 0.17f, 0.85f);
        [SerializeField] Color crossColor = new Color(1f, 0.25f, 0.75f);
        [SerializeField] float crossSize = 5f;
        [SerializeField] float crossThickness = 2.5f;

        EchoRecording recording;
        int length, loopTicks = 1, cutAt = -1;
        Color trackColor = Color.white;

        public void SetData(EchoRecording rec, int visibleLength, int totalTicks, int vanishedAt, Color color)
        {
            if (rec == recording && visibleLength == length && totalTicks == loopTicks && vanishedAt == cutAt && color == trackColor) return;
            recording = rec;
            length = visibleLength;
            loopTicks = Mathf.Max(1, totalTicks);
            cutAt = vanishedAt;
            trackColor = color;
            SetVerticesDirty();
        }

        /// <summary>Horizontal position, in this graphic's local space, of a tick.</summary>
        public float TickToLocalX(float tick)
        {
            var r = rectTransform.rect;
            return r.xMin + r.width * Mathf.Clamp(tick, 0f, loopTicks) / loopTicks;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (recording == null) return;
            var r = GetPixelAdjustedRect();
            float cy = r.center.y;
            float step = r.width / loopTicks;
            float X(float k) => r.xMin + step * Mathf.Clamp(k, 0, loopTicks);

            var faint = trackColor; faint.a *= baseAlpha;
            Quad(vh, X(0), cy - baseThickness * 0.5f, X(length), cy + baseThickness * 0.5f, faint);

            var solid = trackColor; solid.a *= moveAlpha;
            float half = Mathf.Min(moveThickness, r.height - 2f) * 0.5f;
            for (int k = 0; k < length; k++)
                if (!recording[k].IsWait) Quad(vh, X(k), cy - half, X(k) + Mathf.Max(1f, step - 0.5f), cy + half, solid);

            if (cutAt >= 0)
            {
                float xc = X(cutAt);
                Quad(vh, xc, r.yMin, r.xMax, r.yMax, cutColor);
                Line(vh, new Vector2(xc - crossSize, cy - crossSize), new Vector2(xc + crossSize, cy + crossSize), crossThickness, crossColor);
                Line(vh, new Vector2(xc - crossSize, cy + crossSize), new Vector2(xc + crossSize, cy - crossSize), crossThickness, crossColor);
            }
        }

        static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            if (x1 <= x0 || y1 <= y0) return;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
        {
            var n = Vector2.Perpendicular((b - a).normalized) * (thickness * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
