using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>
    /// A menu entry: a label, an optional key hint, and a marker shown while selected. Hovering selects it, so
    /// mouse and keyboard always agree on what is highlighted.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class MenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text keyHint;
        [SerializeField, Tooltip("Shown only while selected.")] GameObject selectedMarker;
        [SerializeField] Color labelNormal = new Color(0.6f, 0.62f, 0.78f);
        [SerializeField] Color labelSelected = Color.white;
        [SerializeField] Color labelDisabled = new Color(0.17f, 0.19f, 0.38f);

        Button button;
        bool selected;

        public Button Button => button ? button : button = GetComponent<Button>();

        public string Label
        {
            get => label ? label.text : string.Empty;
            set { if (label) label.text = value; }
        }

        public string KeyHint
        {
            set
            {
                if (!keyHint) return;
                keyHint.text = value;
                keyHint.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        void Awake() => Button.onClick.AddListener(UIEvents.RaiseActivated);
        void OnEnable() => Refresh();

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            UIEvents.RaiseNavigated();
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Button.interactable && EventSystem.current && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void Update()
        {
            // interactable can change from outside (e.g. "Erase last echo" with no echoes)
            Refresh();
        }

        void Refresh()
        {
            bool usable = Button.interactable;
            bool showSelected = selected && usable;
            if (selectedMarker && selectedMarker.activeSelf != showSelected) selectedMarker.SetActive(showSelected);
            var c = !usable ? labelDisabled : showSelected ? labelSelected : labelNormal;
            if (label) label.color = c;
            if (keyHint) keyHint.color = !usable ? labelDisabled : labelNormal;
        }
    }
}
