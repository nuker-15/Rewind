using System;
using Rewind.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>One cassette on the tape shelf.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class LevelCard : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] TMP_Text number;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text parLabel;
        [SerializeField] TMP_Text bestLabel;
        [SerializeField, Tooltip("Outline shown while selected.")] GameObject selectedFrame;
        [SerializeField, Tooltip("Spun while selected.")] RectTransform[] reels;
        [SerializeField] float reelSpeed = -200f;
        [SerializeField] Color bestAtPar = new Color(1f, 0.71f, 0.28f);
        [SerializeField] Color bestOverPar = new Color(0.77f, 1f, 0.42f);
        [SerializeField] Color unplayed = new Color(0.6f, 0.62f, 0.78f);

        Button button;
        bool selected;
        Action onChosen;

        public Button Button => button ? button : button = GetComponent<Button>();

        void Awake()
        {
            Button.onClick.AddListener(() =>
            {
                UIEvents.RaiseActivated();
                onChosen?.Invoke();
            });
        }

        public void Setup(int index, LevelData level, Action chosen)
        {
            onChosen = chosen;
            if (number) number.text = (index + 1).ToString("00");
            if (title) title.text = level.DisplayName.ToUpperInvariant();
            int par = level.TotalPar;
            if (parLabel) parLabel.text = $"PAR {par}";
            int best = PlayerProgress.GetBest(level);
            if (bestLabel)
            {
                if (best <= 0) { bestLabel.text = "UNPLAYED"; bestLabel.color = unplayed; }
                else if (best <= par) { bestLabel.text = $"BEST {best}"; bestLabel.color = bestAtPar; }
                else { bestLabel.text = $"BEST {best}"; bestLabel.color = bestOverPar; }
            }
            SetSelected(false);
        }

        public void OnSelect(BaseEventData eventData) { SetSelected(true); UIEvents.RaiseNavigated(); }
        public void OnDeselect(BaseEventData eventData) => SetSelected(false);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void SetSelected(bool on)
        {
            selected = on;
            if (selectedFrame) selectedFrame.SetActive(on);
        }

        void Update()
        {
            if (!selected || reels == null) return;
            foreach (var r in reels) if (r) r.Rotate(0f, 0f, reelSpeed * Time.unscaledDeltaTime);
        }
    }
}
