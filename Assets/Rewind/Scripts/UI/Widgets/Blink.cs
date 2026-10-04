using UnityEngine;

namespace Rewind.UI
{
    /// <summary>Blinks a CanvasGroup like a VCR display. Uses unscaled time so it keeps blinking while paused.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Blink : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float period = 1f;
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.35f;

        CanvasGroup group;

        void Awake() => group = GetComponent<CanvasGroup>();

        void Update()
        {
            group.alpha = Mathf.Repeat(Time.unscaledTime, period) < period * 0.5f ? 1f : dimAlpha;
        }
    }
}
