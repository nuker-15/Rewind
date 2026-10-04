using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>A short message that fades out on its own, such as an echo vanishing.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField, Tooltip("Tinted with the message colour.")] Graphic accent;
        [SerializeField] float seconds = 3.6f;
        [SerializeField] float fadeSeconds = 0.4f;

        CanvasGroup group;
        float remaining;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        public void Show(string heading, string message, Color color)
        {
            if (!group) group = GetComponent<CanvasGroup>();
            if (title) { title.text = heading; title.color = color; }
            if (body)
            {
                body.text = message;
                body.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
            if (accent) accent.color = color;
            remaining = seconds;
            group.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            remaining = 0f;
            if (group) group.alpha = 0f;
        }

        void Update()
        {
            if (remaining <= 0f) return;
            remaining -= Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(remaining / fadeSeconds);
        }
    }
}
