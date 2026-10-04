using System.Collections.Generic;
using Rewind.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rewind.UI
{
    /// <summary>The tape shelf. Cards are made from the LevelCard prefab, one per level in the catalog.</summary>
    public sealed class LevelSelectScreen : UIScreen
    {
        [SerializeField] LevelCard cardPrefab;
        [SerializeField, Tooltip("Parent of the cards; should have a GridLayoutGroup.")] RectTransform grid;
        [SerializeField] MenuButton backButton;
        [SerializeField] TMP_Text progressLabel;

        readonly List<LevelCard> cards = new List<LevelCard>();

        void Awake()
        {
            if (backButton) backButton.Button.onClick.AddListener(() => router.ShowTitle());
        }

        protected override void OnOpened()
        {
            var catalog = Game ? Game.Catalog : null;
            if (!catalog || !cardPrefab || !grid) return;
            while (cards.Count < catalog.Count) cards.Add(Instantiate(cardPrefab, grid));
            int solved = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < catalog.Count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                int index = i;
                cards[i].Setup(index, catalog[index], () => router.PlayLevel(index));
                if (PlayerProgress.IsSolved(catalog[index])) solved++;
            }
            if (progressLabel) progressLabel.text = $"{solved} OF {catalog.Count} SOLVED";
        }

        public override void SelectFirst()
        {
            var es = EventSystem.current;
            if (!es || cards.Count == 0) { base.SelectFirst(); return; }
            int index = router.LastPlayedIndex >= 0 ? router.LastPlayedIndex : Game.FirstUnsolvedIndex();
            index = Mathf.Clamp(index, 0, cards.Count - 1);
            es.SetSelectedGameObject(cards[index].gameObject);
        }
    }
}
