using Rewind.Data;
using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>The player or an echo. The same script drives both; the prefabs decide how each looks.</summary>
    public sealed class BodyView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer fill;
        [SerializeField] SpriteRenderer outline;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] Transform eyeLeft;
        [SerializeField] Transform eyeRight;
        [SerializeField, Tooltip("Blinking dot shown while recording (player only).")] SpriteRenderer recordDot;
        [SerializeField, Tooltip("Number badge (echoes only).")] Transform badge;
        [SerializeField] TMP_Text badgeLabel;
        [SerializeField, Range(0f, 1f)] float fillAlpha = 1f;
        [SerializeField, Range(0f, 1f)] float glowAlpha = 0.35f;
        [SerializeField] float eyeShift = 0.08f;
        [SerializeField] float badgeStackOffset = 0.2f;
        [SerializeField] float recordBlinkSeconds = 0.4f;

        Vector3 eyeLeftHome, eyeRightHome, badgeHome;
        bool homesCaptured;
        bool recording;

        void CaptureHomes()
        {
            if (homesCaptured) return;
            if (eyeLeft) eyeLeftHome = eyeLeft.localPosition;
            if (eyeRight) eyeRightHome = eyeRight.localPosition;
            if (badge) badgeHome = badge.localPosition;
            homesCaptured = true;
        }

        /// <param name="echoNumber">1-based echo number, or 0 for the player.</param>
        public void Setup(Color color, int echoNumber, BoardPalette palette)
        {
            CaptureHomes();
            if (fill) { var c = color; c.a = fillAlpha; fill.color = c; }
            if (outline) outline.color = color;
            if (glow) { var c = color; c.a = glowAlpha; glow.color = c; }
            var eyeColor = echoNumber > 0 ? color : palette.background;
            if (eyeLeft && eyeLeft.TryGetComponent(out SpriteRenderer l)) l.color = eyeColor;
            if (eyeRight && eyeRight.TryGetComponent(out SpriteRenderer r)) r.color = eyeColor;
            if (recordDot) { recordDot.color = palette.record; recordDot.enabled = false; }
            if (badgeLabel)
            {
                badgeLabel.text = echoNumber > 0 ? echoNumber.ToString() : string.Empty;
                badgeLabel.color = color;
            }
            if (badge) badge.gameObject.SetActive(echoNumber > 0);
        }

        /// <param name="gridDir">Facing in grid space (y down).</param>
        public void SetFacing(Vector2Int gridDir)
        {
            CaptureHomes();
            var shift = new Vector3(gridDir.x, -gridDir.y, 0f) * eyeShift;
            if (eyeLeft) eyeLeft.localPosition = eyeLeftHome + shift;
            if (eyeRight) eyeRight.localPosition = eyeRightHome + shift;
        }

        public void SetStackIndex(int index)
        {
            CaptureHomes();
            if (badge) badge.localPosition = badgeHome + new Vector3(index * badgeStackOffset, 0f, 0f);
        }

        public void SetRecording(bool on) => recording = on;

        void Update()
        {
            if (!recordDot) return;
            recordDot.enabled = recording && Mathf.FloorToInt(Time.time / recordBlinkSeconds) % 2 == 0;
        }
    }
}
