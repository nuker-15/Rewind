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
        [SerializeField, Tooltip("Optional. When empty, a copy of the tape button is added under it.")] MenuButton editorButton;
        [SerializeField] TMP_Text progressLabel;

        void Awake()
        {
            // the level editor entry is made from the tape button, so the title prefab needs no changes
            if (!editorButton && selectButton)
            {
                editorButton = Instantiate(selectButton, selectButton.transform.parent);
                editorButton.transform.SetSiblingIndex(selectButton.transform.GetSiblingIndex() + 1);
                editorButton.name = "LEVEL EDITOR";
                editorButton.Label = "LEVEL EDITOR";
                editorButton.KeyHint = "";
            }
            if (editorButton) editorButton.Button.onClick.AddListener(() => router.OpenEditor());

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
