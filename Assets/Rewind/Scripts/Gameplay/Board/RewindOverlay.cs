using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>The blue VHS tint and double-arrow shown over the board while time runs backwards.</summary>
    public sealed class RewindOverlay : MonoBehaviour
    {
        [SerializeField] SpriteRenderer tint;
        [SerializeField] Transform icon;
        [SerializeField] SpriteRenderer[] iconParts;
        [SerializeField, Range(0f, 1f)] float tintAlpha = 0.14f;
        [SerializeField, Range(0f, 1f)] float iconAlpha = 0.28f;
        [SerializeField] float pulseSpeed = 6f;

        public void Fit(Bounds bounds, Color color)
        {
            transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
            if (tint)
            {
                tint.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
                var c = color; c.a = tintAlpha; tint.color = c;
            }
            if (icon) icon.localScale = Vector3.one * Mathf.Min(bounds.size.x, bounds.size.y) * 0.18f;
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (iconParts == null) return;
            float a = iconAlpha * (0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed));
            foreach (var part in iconParts)
            {
                if (!part) continue;
                var c = part.color; c.a = a; part.color = c;
            }
        }
    }
}
