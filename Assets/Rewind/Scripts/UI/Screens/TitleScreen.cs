using Rewind.Data;
using TMPro;
using UnityEngine;

namespace Rewind.UI
{
    public sealed class TitleScreen : UIScreen
    {
        [SerializeField] MenuButton continueButton;
        [SerializeField] MenuButton selectButton;
        [SerializeField] MenuButton howToButton;
        [SerializeField] MenuButton fullscreenButton;
        [SerializeField] MenuButton soundButton;
        [SerializeField] MenuButton quitButton;
        [SerializeField] TMP_Text progressLabel;

        void Awake()
        {
            if (continueButton) continueButton.Button.onClick.AddListener(() => router.Continue());
            if (selectButton) selectButton.Button.onClick.AddListener(() => router.ShowLevelSelect());
            if (howToButton) howToButton.Button.onClick.AddListener(() => router.OpenHowTo());
            if (fullscreenButton) fullscreenButton.Button.onClick.AddListener(() => router.ToggleFullscreen());
            if (soundButton) soundButton.Button.onClick.AddListener(() => router.ToggleMute());
            if (quitButton) quitButton.Button.onClick.AddListener(() => router.Quit());
#if UNITY_WEBGL && !UNITY_EDITOR
            if (quitButton) quitButton.gameObject.SetActive(false);
#endif
        }

        protected override void OnTick()
        {
            var catalog = Game ? Game.Catalog : null;
            int solved = 0, total = catalog ? catalog.Count : 0;
            for (int i = 0; i < total; i++) if (PlayerProgress.IsSolved(catalog[i])) solved++;
            if (continueButton) continueButton.Label = solved > 0 ? "CONTINUE" : "START";
            if (fullscreenButton) fullscreenButton.Label = GameSettings.Fullscreen ? "WINDOWED" : "FULLSCREEN";
            if (soundButton) soundButton.Label = GameSettings.Muted ? "SOUND: OFF" : "SOUND: ON";
            if (progressLabel) progressLabel.text = $"{solved} OF {total} TAPES SOLVED";
        }
    }
}
