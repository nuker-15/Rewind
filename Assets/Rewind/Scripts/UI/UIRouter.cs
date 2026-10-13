using System.Collections;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rewind.UI
{
    /// <summary>
    /// Decides which screens are showing. It is the only UI piece that talks to <see cref="RewindGame"/> about flow;
    /// individual screens ask the router to navigate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIRouter : MonoBehaviour
    {
        [SerializeField] RewindGame game;

        [Header("Screens")]
        [SerializeField] TitleScreen title;
        [SerializeField] LevelSelectScreen levelSelect;
        [SerializeField] HudScreen hud;

        [Header("Overlays")]
        [SerializeField] PausePanel pause;
        [SerializeField] SolvedPanel solved;
        [SerializeField] HowToPlayPanel howTo;

        UIScreen howToReturn;
        Coroutine unlockRoutine;
        int overlayClosedFrame = -1;
        LevelEditorScreen editor;   // built in code at start-up, so it needs no prefab

        public RewindGame Game => game;
        /// <summary>A level from the in-game editor is being test-played; "back" returns to the editor.</summary>
        public bool TestingCustomLevel => game && game.IsCustomLevel;
        /// <summary>The tape played most recently, so the shelf can reselect it.</summary>
        public int LastPlayedIndex { get; private set; } = -1;
        public bool AnyOverlayOpen => Is(pause) || Is(solved) || Is(howTo);
        public bool InGame => game && game.HasLevel && Is(hud);

        static bool Is(UIScreen s) => s && s.IsOpen;

        void Awake()
        {
            if (!editor) editor = LevelEditorScreen.Build(this);
        }

        void OnEnable()
        {
            if (!game) return;
            game.LevelLoaded += OnLevelLoaded;
            game.Solved += OnSolved;
        }

        void OnDisable()
        {
            if (!game) return;
            game.LevelLoaded -= OnLevelLoaded;
            game.Solved -= OnSolved;
        }

        void Start()
        {
            GameSettings.Apply();
            if (game && game.HasLevel) OnLevelLoaded(game.CurrentLevel, game.LevelIndex);
            else ShowTitle();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && InGame && !AnyOverlayOpen && game.Phase == Core.LoopPhase.Playing) OpenPause();
        }

        // ---- Menus -----------------------------------------------------------------------------

        public void ShowTitle()
        {
            if (game) game.UnloadLevel();
            CloseOverlays();
            Close(hud);
            Close(levelSelect);
            Close(editor);
            Open(title);
            LockInput();
        }

        public void ShowLevelSelect()
        {
            // after test-playing a level from the editor, "select tape" goes back to the editor instead
            if (TestingCustomLevel)
            {
                OpenEditor();
                return;
            }
            if (game) game.UnloadLevel();
            CloseOverlays();
            Close(hud);
            Close(title);
            Open(levelSelect);
            LockInput();
        }

        public void PlayLevel(int index) => game.LoadLevel(index);
        public void Continue() => PlayLevel(game.FirstUnsolvedIndex());

        public void OpenEditor()
        {
            if (!editor) return;
            CloseOverlays();
            Close(hud);
            Close(title);
            Close(levelSelect);
            Open(editor);
            LockInput();
        }

        /// <summary>Test-plays a level built in the editor. Pause and the solved panel lead back to the editor.</summary>
        public void PlayCustom(LevelData level)
        {
            Close(editor);
            game.LoadCustomLevel(level);
        }

        public void OpenHowTo()
        {
            howToReturn = Is(pause) ? pause : Is(levelSelect) ? levelSelect : Is(title) ? (UIScreen)title : null;
            Open(howTo);
            LockInput();
        }

        public void CloseHowTo()
        {
            if (!Is(howTo)) return;
            overlayClosedFrame = Time.frameCount;
            Close(howTo);
            if (howToReturn) howToReturn.SelectFirst();
            else UnlockNextFrame();
            howToReturn = null;
        }

        public void ToggleFullscreen() => GameSettings.Fullscreen = !GameSettings.Fullscreen;
        public void ToggleMute() => GameSettings.Muted = !GameSettings.Muted;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- In game ---------------------------------------------------------------------------

        public void OpenPause()
        {
            if (!InGame || AnyOverlayOpen) return;
            game.SetPaused(true);
            Open(pause);
            LockInput();
        }

        public void ClosePause()
        {
            if (!Is(pause)) return;
            overlayClosedFrame = Time.frameCount;
            Close(pause);
            game.SetPaused(false);
            UnlockNextFrame();
        }

        public void TogglePause()
        {
            if (Time.frameCount == overlayClosedFrame) return;
            if (Is(pause)) ClosePause(); else OpenPause();
        }

        public void RetryLoop() { ClosePause(); Close(solved); game.RetryLoop(); UnlockNextFrame(); }

        public void NextLevel()
        {
            Close(solved);
            if (!game.LoadNextLevel()) ShowLevelSelect();
        }

        /// <summary>What Escape does depends on what is showing.</summary>
        public void HandleBack()
        {
            // the same key press may already have closed an overlay this frame
            if (Time.frameCount == overlayClosedFrame) return;
            if (Is(howTo)) CloseHowTo();
            else if (Is(pause)) ClosePause();
            else if (Is(solved)) ShowLevelSelect();
            else if (Is(editor)) editor.HandleBack();
            else if (InGame) OpenPause();
            else if (Is(levelSelect)) ShowTitle();
        }

        // ---- Game events -------------------------------------------------------------------------

        void OnLevelLoaded(LevelData level, int index)
        {
            if (Is(editor)) return;   // the editor loads its level to show it on the board; it stays on screen
            if (index >= 0) LastPlayedIndex = index;
            Close(title);
            Close(levelSelect);
            CloseOverlays();
            Open(hud);
            // The title / level select screens lock input; release it now that the board is live.
            UnlockNextFrame();
        }

        void OnSolved(SolveResult result)
        {
            if (TestingCustomLevel && editor) editor.RecordSolve(result.Loops);
            if (!solved) return;
            solved.Configure(result, game.CurrentLevel, game.HasNextLevel);
            Open(solved);
            LockInput();
        }

        // ---- Helpers --------------------------------------------------------------------------------

        void CloseOverlays()
        {
            Close(pause);
            Close(solved);
            Close(howTo);
            if (game) game.SetPaused(false);
        }

        static void Open(UIScreen s) { if (s) s.Open(); }
        static void Close(UIScreen s) { if (s) s.Close(); }

        void LockInput()
        {
            if (game) game.InputLocked = true;
        }

        // Unlock a frame later so the key that closed an overlay does not also move the player.
        void UnlockNextFrame()
        {
            if (unlockRoutine != null) StopCoroutine(unlockRoutine);
            unlockRoutine = StartCoroutine(UnlockAfterFrame());
        }

        IEnumerator UnlockAfterFrame()
        {
            yield return null;
            unlockRoutine = null;
            if (!game || AnyOverlayOpen || !Is(hud)) yield break;
            game.InputLocked = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
