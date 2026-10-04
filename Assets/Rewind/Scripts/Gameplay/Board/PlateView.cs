using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class PlateView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer fill;
        [SerializeField] SpriteRenderer frame;
        [SerializeField] SpriteRenderer glow;
        [SerializeField, Range(0f, 1f)] float idleFillAlpha = 0.1f;
        [SerializeField, Range(0f, 1f)] float pressedFillAlpha = 0.5f;
        [SerializeField, Range(0f, 1f)] float glowAlpha = 0.45f;

        Color color = Color.white;
        bool? pressed;

        public void Setup(Color channelColor)
        {
            color = channelColor;
            if (frame) frame.color = channelColor;
            pressed = null;
            SetPressed(false);
        }

        public void SetPressed(bool on)
        {
            if (pressed == on) return;
            pressed = on;
            if (fill) fill.color = WithAlpha(color, on ? pressedFillAlpha : idleFillAlpha);
            if (glow)
            {
                glow.enabled = on;
                glow.color = WithAlpha(color, glowAlpha);
            }
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
