using Rewind.Core;
using TMPro;
using UnityEngine;

namespace Rewind.UI
{
    public sealed class PausePanel : UIScreen
    {
        [SerializeField] MenuButton resume;
        [SerializeField] MenuButton retry;
        [SerializeField] MenuButton erase;
        [SerializeField] MenuButton restart;
        [SerializeField] MenuButton hint;
        [SerializeField] MenuButton howTo;
        [SerializeField] MenuButton levelSelect;
        [SerializeField] MenuButton title;
        [SerializeField] TMP_Text footer;

        void Awake()
        {
            if (resume) resume.Button.onClick.AddListener(() => router.ClosePause());
            if (retry) retry.Button.onClick.AddListener(() => router.RetryLoop());
            if (erase) erase.Button.onClick.AddListener(() => router.EraseLastEcho());
            if (restart) restart.Button.onClick.AddListener(() => router.RestartLevel());
            if (hint) hint.Button.onClick.AddListener(() => { router.ClosePause(); router.ShowHint(); });
            if (howTo) howTo.Button.onClick.AddListener(() => router.OpenHowTo());
            if (levelSelect) levelSelect.Button.onClick.AddListener(() => router.ShowLevelSelect());
            if (title) title.Button.onClick.AddListener(() => router.ShowTitle());
        }

        protected override void OnTick()
        {
            var game = Game;
            if (!game || !game.HasLevel) return;
            if (retry) retry.Button.interactable = game.Phase == LoopPhase.Playing;
            if (erase) erase.Button.interactable = game.CanErase;
            if (restart) restart.Button.interactable = game.CanRestart;
            if (footer) footer.text = $"TAPE {game.LevelIndex + 1:00}  {game.CurrentLevel.DisplayName.ToUpperInvariant()}";
        }
    }
}
