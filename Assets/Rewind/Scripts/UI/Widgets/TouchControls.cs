using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>On-screen d-pad and loop buttons, shown only when a touchscreen is present (or when forced).</summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] UIRouter router;
        [SerializeField] GameObject root;
        [SerializeField] Button endLoop;
        [SerializeField] Button retry;
        [SerializeField] Button erase;
        [SerializeField] bool forceVisible;

        public bool Visible => forceVisible || Touchscreen.current != null;

        void Awake()
        {
            if (endLoop) endLoop.onClick.AddListener(() => router.Game.EndLoopEarly());
            if (retry) retry.onClick.AddListener(() => router.Game.RetryLoop());
            if (erase) erase.onClick.AddListener(() => router.Game.EraseLastEcho());
        }

        void Update()
        {
            bool show = Visible;
            if (root && root.activeSelf != show) root.SetActive(show);
            if (!show || !router || !router.Game) return;
            var game = router.Game;
            bool live = !game.InputLocked && !game.IsPaused;
            if (endLoop) endLoop.interactable = live && game.CanEndLoop;
            if (retry) retry.interactable = live && game.Phase == Core.LoopPhase.Playing;
            if (erase) erase.interactable = live && game.CanErase;
        }
    }
}
