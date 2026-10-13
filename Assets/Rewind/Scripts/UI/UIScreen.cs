using Rewind.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>Base for every screen and overlay: fades a CanvasGroup in and out and picks the first selection.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [SerializeField] protected UIRouter router;
        [SerializeField, Tooltip("Selected when the screen opens, for keyboard and gamepad navigation.")]
        protected Selectable firstSelected;
        [SerializeField, Min(0f)] float fadeSeconds = 0.12f;

        CanvasGroup group;
        float targetAlpha;

        public bool IsOpen { get; private set; }
        protected RewindGame Game => router ? router.Game : null;

        CanvasGroup Group => group ? group : group = GetComponent<CanvasGroup>();

        /// <summary>For screens built in code rather than in a prefab.</summary>
        internal void Bind(UIRouter owner) => router = owner;

        public void Open()
        {
            gameObject.SetActive(true);
            IsOpen = true;
            targetAlpha = 1f;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            if (fadeSeconds <= 0f) Group.alpha = 1f;
            OnOpened();
            SelectFirst();
        }

        public void Close()
        {
            if (!IsOpen && !gameObject.activeSelf) return;
            bool wasOpen = IsOpen;
            IsOpen = false;
            targetAlpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            if (wasOpen) OnClosed();
            if (fadeSeconds <= 0f || !gameObject.activeInHierarchy) CloseImmediate();
        }

        public void CloseImmediate()
        {
            IsOpen = false;
            targetAlpha = 0f;
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public virtual void SelectFirst()
        {
            var es = EventSystem.current;
            if (!es) return;
            bool usable = firstSelected && firstSelected.isActiveAndEnabled && firstSelected.interactable;
            es.SetSelectedGameObject(usable ? firstSelected.gameObject : null);
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
        /// <summary>Called every frame while the screen is open.</summary>
        protected virtual void OnTick() { }

        void Update()
        {
            var g = Group;
            if (!Mathf.Approximately(g.alpha, targetAlpha))
                g.alpha = Mathf.MoveTowards(g.alpha, targetAlpha, Time.unscaledDeltaTime / Mathf.Max(0.0001f, fadeSeconds));
            else if (!IsOpen && gameObject.activeSelf)
                gameObject.SetActive(false);
            if (IsOpen) OnTick();
        }
    }
}
