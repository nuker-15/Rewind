using System;
using System.Collections.Generic;
using Rewind.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rewind.Gameplay
{
    /// <summary>
    /// Feeds an action map into <see cref="RewindGame"/>. Rebind keys or add gamepad bindings in the
    /// input actions asset; the action names below are the only contract.
    /// </summary>
    public sealed class GameplayInput : MonoBehaviour
    {
        [SerializeField] RewindGame game;
        [SerializeField] InputActionAsset actions;
        [SerializeField] string mapName = "Gameplay";

        [Header("Action names")]
        [SerializeField] string moveUp = "MoveUp";
        [SerializeField] string moveDown = "MoveDown";
        [SerializeField] string moveLeft = "MoveLeft";
        [SerializeField] string moveRight = "MoveRight";
        [SerializeField] string endLoop = "EndLoop";
        [SerializeField] string retryLoop = "RetryLoop";
        [SerializeField] string eraseEcho = "EraseEcho";
        [SerializeField] string startLoop = "StartLoop";

        readonly List<(InputAction action, Action<InputAction.CallbackContext> pressed, Action<InputAction.CallbackContext> released)> hooks =
            new List<(InputAction, Action<InputAction.CallbackContext>, Action<InputAction.CallbackContext>)>();
        InputActionMap map;

        bool Ready => game && !game.IsPaused && !game.InputLocked;

        void OnEnable()
        {
            if (!game || !actions)
            {
                Debug.LogWarning("GameplayInput needs a RewindGame and an InputActionAsset.", this);
                return;
            }
            map = actions.FindActionMap(mapName, false);
            if (map == null)
            {
                Debug.LogError($"Action map '{mapName}' not found in {actions.name}.", this);
                return;
            }
            HookMove(moveUp, Dir.Up, 1);
            HookMove(moveDown, Dir.Down, 2);
            HookMove(moveLeft, Dir.Left, 3);
            HookMove(moveRight, Dir.Right, 4);
            HookPress(endLoop, () => game.EndLoopEarly());
            HookPress(retryLoop, () => game.RetryLoop());
            HookPress(eraseEcho, () => game.EraseLastEcho());
            HookPress(startLoop, () => { if (game.AcceptsMoves) game.BeginLoop(); });
            map.Enable();
        }

        void OnDisable()
        {
            foreach (var (action, pressed, released) in hooks)
            {
                if (pressed != null) action.performed -= pressed;
                if (released != null) action.canceled -= released;
            }
            hooks.Clear();
            map?.Disable();
            map = null;
        }

        void HookMove(string actionName, Vector2Int dir, int sourceId)
        {
            var action = Find(actionName);
            if (action == null) return;
            Action<InputAction.CallbackContext> pressed = _ => game.PressDirection(dir, sourceId);
            Action<InputAction.CallbackContext> released = _ => game.ReleaseDirection(sourceId);
            action.performed += pressed;
            action.canceled += released;
            hooks.Add((action, pressed, released));
        }

        void HookPress(string actionName, Action command)
        {
            var action = Find(actionName);
            if (action == null) return;
            Action<InputAction.CallbackContext> pressed = _ => { if (Ready) command(); };
            action.performed += pressed;
            hooks.Add((action, pressed, null));
        }

        InputAction Find(string actionName)
        {
            var action = map.FindAction(actionName, false);
            if (action == null) Debug.LogWarning($"Action '{actionName}' not found in map '{mapName}'.", this);
            return action;
        }
    }
}
