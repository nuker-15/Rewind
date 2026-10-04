using System;
using Rewind.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Rewind.UI
{
    /// <summary>The title card shown when a tape starts, and again as the hint. Any key or click closes it.</summary>
    public sealed class IntroPanel : UIScreen
    {
        [SerializeField] TMP_Text tapeLabel;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] TMP_Text parLabel;
        [SerializeField] TMP_Text promptLabel;
        [SerializeField, Tooltip("Presses this soon after opening are ignored, so a double tap does not skip the card.")]
        float ignoreInputSeconds = 0.25f;

        IDisposable listener;
        float openedAt;

        public void Configure(LevelData level, int index, int total, bool asHint)
        {
            if (!level) return;
            if (tapeLabel) tapeLabel.text = $"TAPE {index + 1:00} OF {total:00}";
            if (nameLabel) nameLabel.text = level.DisplayName.ToUpperInvariant();
            if (hintLabel) hintLabel.text = level.Hint;
            float seconds = Game && Game.Config ? Game.Config.LoopSeconds : 10f;
            if (parLabel) parLabel.text = $"PAR {level.Par} LOOPS   ·   {seconds:0} SECONDS EACH";
            bool touch = Touchscreen.current != null;
            if (promptLabel) promptLabel.text = touch ? "TAP TO CONTINUE" : asHint ? "PRESS ANY KEY TO RESUME" : "PRESS ANY KEY";
        }

        protected override void OnOpened()
        {
            openedAt = Time.unscaledTime;
            Listen();
        }

        protected override void OnClosed()
        {
            listener?.Dispose();
            listener = null;
        }

        void OnDestroy() => listener?.Dispose();

        void Listen()
        {
            listener?.Dispose();
            listener = InputSystem.onAnyButtonPress.CallOnce(OnAnyPress);
        }

        void OnAnyPress(InputControl control)
        {
            listener = null;
            if (!IsOpen) return;
            if (Time.unscaledTime - openedAt < ignoreInputSeconds) { Listen(); return; }
            router.CloseIntro();
        }
    }
}
