using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rewind.UI
{
    /// <summary>Keys that work everywhere: back/menu, hint, fullscreen and mute. Bindings live in the input actions asset.</summary>
    public sealed class ShellInput : MonoBehaviour
    {
        [SerializeField] UIRouter router;
        [SerializeField] InputActionAsset actions;
        [SerializeField] string mapName = "Shell";
        [SerializeField] string back = "Back";
        [SerializeField] string pause = "Pause";
        [SerializeField] string hint = "Hint";
        [SerializeField] string fullscreen = "Fullscreen";
        [SerializeField] string mute = "Mute";

        readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> hooks =
            new List<(InputAction, Action<InputAction.CallbackContext>)>();
        InputActionMap map;

        void OnEnable()
        {
            if (!router || !actions) return;
            map = actions.FindActionMap(mapName, false);
            if (map == null)
            {
                Debug.LogError($"Action map '{mapName}' not found in {actions.name}.", this);
                return;
            }
            Hook(back, router.HandleBack);
            Hook(pause, router.TogglePause);
            Hook(hint, router.ShowHint);
            Hook(fullscreen, router.ToggleFullscreen);
            Hook(mute, router.ToggleMute);
            map.Enable();
        }

        void OnDisable()
        {
            foreach (var (action, handler) in hooks) action.performed -= handler;
            hooks.Clear();
            map?.Disable();
            map = null;
        }

        void Hook(string actionName, Action command)
        {
            var action = map.FindAction(actionName, false);
            if (action == null) return;
            Action<InputAction.CallbackContext> handler = _ => command();
            action.performed += handler;
            hooks.Add((action, handler));
        }
    }
}
