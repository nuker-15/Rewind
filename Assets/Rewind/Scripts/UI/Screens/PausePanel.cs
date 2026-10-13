using UnityEngine;

namespace Rewind.UI
{
    public sealed class PausePanel : UIScreen
    {
        [SerializeField] MenuButton resume;
        [SerializeField] MenuButton howTo;
        [SerializeField] MenuButton levelSelect;
        [SerializeField] MenuButton title;

        void Awake()
        {
            if (resume) resume.Button.onClick.AddListener(() => router.ClosePause());
            if (howTo) howTo.Button.onClick.AddListener(() => router.OpenHowTo());
            if (levelSelect) levelSelect.Button.onClick.AddListener(() => router.ShowLevelSelect());
            if (title) title.Button.onClick.AddListener(() => router.ShowTitle());
        }

        protected override void OnOpened()
        {
            if (levelSelect) levelSelect.Label = router.TestingCustomLevel ? "BACK TO EDITOR" : "SELECT TAPE";
        }
    }
}
