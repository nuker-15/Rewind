using Rewind.Data;
using Rewind.Gameplay;
using TMPro;
using UnityEngine;

namespace Rewind.UI
{
    public sealed class SolvedPanel : UIScreen
    {
        [SerializeField] TMP_Text loopsLabel;
        [SerializeField] TMP_Text verdictLabel;
        [SerializeField] MenuButton next;
        [SerializeField] MenuButton replay;
        [SerializeField] MenuButton levelSelect;
        [SerializeField] Color atParColor = new Color(1f, 0.71f, 0.28f);
        [SerializeField] Color overParColor = new Color(0.6f, 0.62f, 0.78f);

        void Awake()
        {
            if (next) next.Button.onClick.AddListener(() => router.NextLevel());
            if (replay) replay.Button.onClick.AddListener(() => router.RetryLoop());
            if (levelSelect) levelSelect.Button.onClick.AddListener(() => router.ShowLevelSelect());
        }

        public void Configure(SolveResult result, LevelData level, bool hasNext)
        {
            if (loopsLabel) loopsLabel.text = $"{result.Loops} LOOP{(result.Loops == 1 ? "" : "S")}   ·   PAR {result.Par}";
            if (verdictLabel)
            {
                verdictLabel.text = result.MatchedPar
                    ? "★  MATCHED PAR" + (result.NewBest ? "   ·   NEW BEST" : "")
                    : $"PAR IS {result.Par}. THERE IS A SHORTER WAY.";
                verdictLabel.color = result.MatchedPar ? atParColor : overParColor;
            }
            // a level being tested from the editor: "next" leads back to the editor, and there is no shelf to go to
            bool custom = router && router.TestingCustomLevel;
            if (next) next.Label = custom ? "BACK TO EDITOR" : hasNext ? "NEXT TAPE" : "ALL TAPES";
            if (levelSelect) levelSelect.gameObject.SetActive(!custom);
        }
    }
}
