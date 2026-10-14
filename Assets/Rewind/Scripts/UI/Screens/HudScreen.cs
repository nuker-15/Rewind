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

        RoomMap roomMap;

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
            if (Game) Game.SectionCleared += OnSectionCleared;
            if (!roomMap && Game) roomMap = RoomMap.Build((RectTransform)transform, Game, palette);
        }

        protected override void OnClosed()
        {
            if (Game) Game.ParadoxOccurred -= OnParadox;
            if (Game) Game.SectionCleared -= OnSectionCleared;
        }

        void OnDestroy()
        {
            if (Game) Game.ParadoxOccurred -= OnParadox;
            if (Game) Game.SectionCleared -= OnSectionCleared;
        }

        void OnSectionCleared(int section, int loops, int par)
        {
            if (toast) toast.Show($"SECTION {section} CLEAR", $"{loops} LOOP{(loops == 1 ? "" : "S")}   |   PAR {par}", loops <= par ? palette.exit : palette.rewind);
        }

        void OnParadox(ParadoxEvent p)
        {
            // the camera only shows one room, so say when it happened somewhere the player cannot see
            var game = Game;
            var rooms = game.CurrentLevel.RoomsFor(game.Session.Layout);
            var player = game.Session.World.Player.Cell;
            string where = rooms.Exists(r => r.Contains(p.Cell) && r.Contains(player)) ? "" : " IN ANOTHER ROOM";
            if (toast) toast.Show($"ECHO {p.EchoIndex + 1} NEVER EXISTED{where}", ParadoxText.Describe(p.Reason), palette.paradox);
        }

        protected override void OnTick()
        {
            var game = Game;
            if (!game || !game.HasLevel) return;
            var phase = game.Phase;

            if (parLabel) parLabel.text = $"PAR {game.SectionPar}";

            float loopSeconds = game.Session.LoopTicks * game.Config.TickSeconds;
            float seconds = Mathf.Min(loopSeconds, game.ViewSeconds);
            if (timeLabel)
            {
                int tenths = Mathf.FloorToInt(seconds * 10f);   // whole tenths, so 59.96 never shows as 0:60.0
                timeLabel.text = $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
                bool late = phase == LoopPhase.Playing && seconds > loopSeconds - 3f;
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
