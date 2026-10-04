using Rewind.Core;
using Rewind.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>The in-game heads-up display: VCR status, par, tape name, time code, buttons and the timeline.</summary>
    public sealed class HudScreen : UIScreen
    {
        [SerializeField] BoardPalette palette;

        [Header("Status")]
        [SerializeField] Image statusIcon;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Sprite recordSprite;
        [SerializeField] Sprite readySprite;
        [SerializeField] Sprite rewindSprite;
        [SerializeField] Sprite stopSprite;
        [SerializeField] TMP_Text parLabel;
        [SerializeField] TMP_Text tapeLabel;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text timeLabel;
        [SerializeField] Color textColor = new Color(0.92f, 0.91f, 0.97f);

        [Header("Buttons")]
        [SerializeField] Button soundButton;
        [SerializeField] Button fullscreenButton;
        [SerializeField] Button pauseButton;
        [SerializeField] Image soundIcon;
        [SerializeField] Sprite soundOnSprite;
        [SerializeField] Sprite soundOffSprite;
        [SerializeField] Image fullscreenIcon;
        [SerializeField] Sprite enterFullscreenSprite;
        [SerializeField] Sprite exitFullscreenSprite;

        [Header("Parts")]
        [SerializeField] ToastView toast;
        [SerializeField] TMP_Text readyPrompt;
        [SerializeField] GameObject keyHints;
        [SerializeField] TouchControls touchControls;

        void Awake()
        {
            // status (REC/READY icon + label) and tape (number + name) are no longer shown
            if (statusIcon) statusIcon.gameObject.SetActive(false);
            if (statusLabel) statusLabel.gameObject.SetActive(false);
            if (tapeLabel) tapeLabel.gameObject.SetActive(false);
            if (nameLabel) nameLabel.gameObject.SetActive(false);

            if (soundButton) soundButton.onClick.AddListener(() => router.ToggleMute());
            if (fullscreenButton) fullscreenButton.onClick.AddListener(() => router.ToggleFullscreen());
            if (pauseButton) pauseButton.onClick.AddListener(() => router.OpenPause());
        }

        protected override void OnOpened()
        {
            if (toast) toast.Hide();
            if (Game) Game.ParadoxOccurred += OnParadox;
        }

        protected override void OnClosed()
        {
            if (Game) Game.ParadoxOccurred -= OnParadox;
        }

        void OnDestroy()
        {
            if (Game) Game.ParadoxOccurred -= OnParadox;
        }

        void OnParadox(ParadoxEvent p)
        {
            if (toast) toast.Show($"ECHO {p.EchoIndex + 1} NEVER EXISTED", ParadoxText.Describe(p.Reason), palette.paradox);
        }

        protected override void OnTick()
        {
            var game = Game;
            if (!game || !game.HasLevel) return;
            var phase = game.Phase;

            var level = game.CurrentLevel;
            if (parLabel) parLabel.text = $"PAR {level.Par}";

            float seconds = Mathf.Min(game.Config.LoopSeconds, game.ViewSeconds);
            if (timeLabel)
            {
                timeLabel.text = $"0:{seconds:00.0}";
                bool late = phase == LoopPhase.Playing && seconds > game.Config.LoopSeconds - 3f;
                timeLabel.color = late ? palette.record : textColor;
            }

            bool touch = touchControls && touchControls.Visible;
            if (keyHints && keyHints.activeSelf == touch) keyHints.SetActive(!touch);
            if (readyPrompt)
            {
                bool show = phase == LoopPhase.Ready && !router.AnyOverlayOpen;
                if (readyPrompt.gameObject.activeSelf != show) readyPrompt.gameObject.SetActive(show);
                if (show)
                {
                    int loop = game.LoopNumber;
                    readyPrompt.text = touch ? $"TAP A DIRECTION TO START LOOP {loop}"
                        : loop == 1 ? "MOVE TO START RECORDING" : $"MOVE TO START LOOP {loop}";
                }
            }

            if (soundIcon) soundIcon.sprite = GameSettings.Muted ? soundOffSprite : soundOnSprite;
            if (fullscreenIcon) fullscreenIcon.sprite = GameSettings.Fullscreen ? exitFullscreenSprite : enterFullscreenSprite;
        }
    }
}
