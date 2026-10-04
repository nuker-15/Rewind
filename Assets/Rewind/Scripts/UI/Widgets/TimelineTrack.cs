using Rewind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>One row of the timeline: a label, the track graphic, and the reason if the echo vanished.</summary>
    public sealed class TimelineTrack : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TimelineTrackGraphic track;
        [SerializeField] TMP_Text reason;
        [SerializeField] LayoutElement layoutElement;
        [SerializeField] float reasonGap = 14f;

        public void Setup(string name, Color color, float height)
        {
            if (label)
            {
                label.text = name;
                label.color = color;
            }
            if (layoutElement)
            {
                layoutElement.preferredHeight = height;
                layoutElement.minHeight = height;
            }
        }

        public void Show(EchoRecording recording, int visibleLength, int loopTicks, int vanishedAt, string why, Color color)
        {
            if (track) track.SetData(recording, visibleLength, loopTicks, vanishedAt, color);
            if (!reason) return;
            bool show = vanishedAt >= 0 && !string.IsNullOrEmpty(why);
            if (reason.gameObject.activeSelf != show) reason.gameObject.SetActive(show);
            if (!show || !track) return;
            reason.text = why;
            // The reason is a child of the track, anchored to its left edge; start it just right of the cut.
            var trackRect = track.rectTransform.rect;
            float x = track.TickToLocalX(vanishedAt) - trackRect.xMin + reasonGap;
            var rt = reason.rectTransform;
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, trackRect.width - x), 0f);
        }
    }
}
