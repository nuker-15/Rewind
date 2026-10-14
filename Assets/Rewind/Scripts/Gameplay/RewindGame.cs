using System;
using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public readonly struct SolveResult
    {
        public readonly int Loops;
        public readonly int Par;
        public readonly bool NewBest;
        public SolveResult(int loops, int par, bool newBest) { Loops = loops; Par = par; NewBest = newBest; }
        public bool MatchedPar => Loops <= Par;
    }

    /// <summary>
    /// Runs the time loop in real time. This is the game's public surface: views, audio and UI subscribe to its
    /// events and call its commands, and it never references any of them.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class RewindGame : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] LevelCatalog catalog;

        /// <summary>A level was loaded; the board should rebuild.</summary>
        public event Action<LevelData, int> LevelLoaded;
        /// <summary>The current level was closed (back to menus).</summary>
        public event Action LevelUnloaded;
        /// <summary>A fresh loop is waiting for the first move. Argument: loop number, starting at 1.</summary>
        public event Action<int> LoopReady;
        /// <summary>The clock started for the current loop.</summary>
        public event Action LoopStarted;
        /// <summary>One tick of the simulation ran.</summary>
        public event Action<TickReport> Ticked;
        public event Action<ParadoxEvent> ParadoxOccurred;
        /// <summary>The loop is playing back in reverse. Argument: seconds the rewind will take.</summary>
        public event Action<float> RewindStarted;
        public event Action RewindFinished;
        public event Action<SolveResult> Solved;
        /// <summary>The player reached a checkpoint: the section number just cleared (1 and up), its loops and its par.</summary>
        public event Action<int, int, int> SectionCleared;
        /// <summary>The player moved on to another section, or went back to one. Closed sections may have changed.</summary>
        public event Action SectionChanged;
        public event Action<bool> PausedChanged;

        readonly List<(int id, Vector2Int dir)> held = new List<(int, Vector2Int)>();
        readonly Queue<Vector2Int> queued = new Queue<Vector2Int>();
        int holdTicks;
        int sectionsShown;      // finished sections as of the last SectionChanged
        float accumulator;
        float stepAge;
        float rewindPos;
        float rewindSecondsPerSnapshot;
        int rewindingLoop;
        Action afterRewind;

        public GameConfig Config => config;
        public LevelCatalog Catalog => catalog;
        public LoopSession Session { get; private set; }
        /// <summary>Set by the camera while it moves between rooms or zooms: the loop's clock does not run meanwhile.</summary>
        public bool ClockHeld { get; set; }
        public LevelData CurrentLevel { get; private set; }
        public int LevelIndex { get; private set; } = -1;
        public bool HasLevel => Session != null;
        /// <summary>The level came from the in-game editor rather than the catalog.</summary>
        public bool IsCustomLevel => Session != null && LevelIndex < 0;
        public bool IsPaused { get; private set; }
        /// <summary>Set by whoever owns overlays (intro cards, menus) to stop moves from reaching the board.</summary>
        public bool InputLocked { get; set; }
        public LoopPhase Phase => Session?.Phase ?? LoopPhase.Ready;
        public int EchoCount => Session?.Echoes.Count ?? 0;
        /// <summary>The loop number shown to the player. During a rewind it still names the loop being rewound.</summary>
        public int LoopNumber => Session == null ? 0 : Phase == LoopPhase.Rewinding ? rewindingLoop : Session.LoopsUsed;
        /// <summary>Snapshot position during a rewind, counting down to zero.</summary>
        public float RewindPosition => rewindPos;
        /// <summary>0 to 1: how far bodies have slid toward their new cells since the last tick.</summary>
        public float StepBlend
        {
            get
            {
                if (Phase != LoopPhase.Playing || IsPaused || config.MoveAnimSeconds <= 0f) return 1f;
                float f = Mathf.Clamp01(stepAge / config.MoveAnimSeconds);
                return 1f - (1f - f) * (1f - f);
            }
        }
        /// <summary>The current time in ticks, smoothed between steps. Drives the time code and timeline playhead.</summary>
        public float ViewTick
        {
            get
            {
                if (Session == null) return 0f;
                switch (Phase)
                {
                    case LoopPhase.Rewinding: return rewindPos;
                    case LoopPhase.Ready: return 0f;
                    case LoopPhase.Solved: return Session.Tick;
                    default:
                        if (Session.Tick == 0) return 0f;
                        return Mathf.Min(Session.LoopTicks, Session.Tick - 1 + Mathf.Clamp01(stepAge / config.TickSeconds));
                }
            }
        }
        public float ViewSeconds => ViewTick * (config ? config.TickSeconds : 0f);

        public bool CanEndLoop => Session != null && Phase == LoopPhase.Playing && Session.Tick > 0;
        public bool CanRetry => Session != null && (Phase == LoopPhase.Playing || Phase == LoopPhase.Solved);
        public bool CanErase => Session != null && Session.CanRemoveEcho && Phase != LoopPhase.Rewinding;
        public bool CanRestart => Session != null && Phase != LoopPhase.Rewinding && (Session.Echoes.Count > 0 || Session.SectionsFinished > 0 || Phase != LoopPhase.Ready);

        /// <summary>Par of a section, by the checkpoint it starts at (-1 for the first section).</summary>
        public int ParFor(int checkpoint) => checkpoint < 0 ? CurrentLevel.Par : Session.Layout.Checkpoints[checkpoint].Par;
        /// <summary>Par of the section the player is in.</summary>
        public int SectionPar => Session == null ? 0 : ParFor(Session.CurrentCheckpoint);
        public bool HasNextLevel => catalog && LevelIndex >= 0 && LevelIndex < catalog.Count - 1;
        public bool AcceptsMoves => Session != null && !IsPaused && !InputLocked && (Phase == LoopPhase.Ready || Phase == LoopPhase.Playing);

        void OnValidate()
        {
            if (!config) Debug.LogWarning("RewindGame needs a GameConfig asset.", this);
            if (!catalog) Debug.LogWarning("RewindGame needs a LevelCatalog asset.", this);
        }

        // ---- Levels -------------------------------------------------------------------

        public void LoadLevel(int index)
        {
            if (!catalog || index < 0 || index >= catalog.Count)
            {
                Debug.LogError($"No level at index {index}.", this);
                return;
            }
            Load(catalog[index], index);
        }

        /// <summary>Loads a level that is not in the catalog, such as one from the in-game editor.</summary>
        public void LoadCustomLevel(LevelData level)
        {
            if (level) Load(level, -1);
        }

        void Load(LevelData level, int index)
        {
            LevelLayout layout;
            try { layout = level.BuildLayout(); }
            catch (FormatException e)
            {
                Debug.LogError($"Level '{level.name}' has a broken map: {e.Message}", level);
                return;
            }
            CurrentLevel = level;
            LevelIndex = index;
            Session = new LoopSession(layout, config.LoopTicksFor(level.LoopSeconds), config.LoopTicksFor);
            sectionsShown = 0;
            ResetTiming();
            SetPaused(false);
            LevelLoaded?.Invoke(CurrentLevel, index);
            LoopReady?.Invoke(Session.LoopsUsed);
        }

        public bool LoadNextLevel()
        {
            if (!HasNextLevel) return false;
            LoadLevel(LevelIndex + 1);
            return true;
        }

        public void UnloadLevel()
        {
            if (Session == null) return;
            Session = null;
            CurrentLevel = null;
            LevelIndex = -1;
            ResetTiming();
            SetPaused(false);
            LevelUnloaded?.Invoke();
        }

        public int FirstUnsolvedIndex()
        {
            if (!catalog) return 0;
            for (int i = 0; i < catalog.Count; i++) if (!PlayerProgress.IsSolved(catalog[i])) return i;
            return 0;
        }

        // ---- Commands -----------------------------------------------------------------

        /// <summary>A direction went down. sourceId lets several inputs (keys, touch buttons) hold directions at once.</summary>
        public void PressDirection(Vector2Int dir, int sourceId)
        {
            if (!AcceptsMoves) return;
            if (queued.Count < config.InputQueueLimit) queued.Enqueue(dir);
            if (!held.Exists(h => h.id == sourceId)) held.Add((sourceId, dir));
            if (Phase == LoopPhase.Ready) BeginLoop();
        }

        public void ReleaseDirection(int sourceId) => held.RemoveAll(h => h.id == sourceId);

        /// <summary>Starts the clock without moving.</summary>
        public void BeginLoop()
        {
            if (Session == null || IsPaused || !Session.Begin()) return;
            accumulator = config.TickSeconds;   // the first step happens on the next frame
            stepAge = config.MoveAnimSeconds;
            LoopStarted?.Invoke();
        }

        public void EndLoopEarly()
        {
            if (CanEndLoop && !IsPaused) EndLoop();
        }

        public void RetryLoop()
        {
            if (CanRetry) StartRewind(StartFreshLoop);
        }

        public void EraseLastEcho()
        {
            if (!CanErase) return;
            void Drop() { Session.RemoveLastEcho(); StartFreshLoop(); }
            if (Phase == LoopPhase.Ready) Drop(); else StartRewind(Drop);
        }

        public void RestartLevel()
        {
            if (!CanRestart) return;
            void Clear() { Session.ClearEchoes(); StartFreshLoop(); }
            if (Phase == LoopPhase.Ready) Clear(); else StartRewind(Clear);
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            ClearHeldInput();
            PausedChanged?.Invoke(paused);
        }

        // ---- Clock ----------------------------------------------------------------------

        void Update()
        {
            if (Session == null || IsPaused) return;
            float dt = Time.deltaTime;
            if (Session.Phase == LoopPhase.Playing)
            {
                stepAge += dt;
                if (ClockHeld) return;   // the camera is moving to another room: time stands still for everyone
                accumulator += dt;
                while (accumulator >= config.TickSeconds && Session.Phase == LoopPhase.Playing)
                {
                    accumulator -= config.TickSeconds;
                    if (Session.IsTimeUp) { EndLoop(); break; }
                    Step();
                }
            }
            else if (Session.Phase == LoopPhase.Rewinding)
            {
                rewindPos -= dt / rewindSecondsPerSnapshot;
                if (rewindPos <= 0f) FinishRewind();
            }
        }

        void Step()
        {
            var report = Session.Step(ConsumeDirection());
            stepAge = 0f;
            Ticked?.Invoke(report);
            foreach (var p in report.Paradoxes) ParadoxOccurred?.Invoke(p);
            if (report.ReachedCheckpoint)
            {
                // the session already waits at the checkpoint in a fresh section; a held key must be pressed again to start
                ResetTiming();
                int last = Session.SectionsFinished - 1;
                SectionCleared?.Invoke(Session.SectionsFinished, Session.FinishedLoops(last), ParFor(Session.FinishedCheckpoint(last)));
                NotifySection();
                LoopReady?.Invoke(Session.LoopsUsed);
            }
            else if (Session.Phase == LoopPhase.Solved)
            {
                ClearHeldInput();
                bool newBest = !IsCustomLevel && PlayerProgress.Record(CurrentLevel, Session.LoopsUsed);
                Solved?.Invoke(new SolveResult(Session.LoopsUsed, CurrentLevel.TotalPar, newBest));
            }
        }

        Vector2Int? ConsumeDirection()
        {
            if (queued.Count > 0)
            {
                holdTicks = 0;
                return queued.Dequeue();
            }
            if (held.Count > 0)
            {
                if (++holdTicks >= config.RepeatDelayTicks) return held[held.Count - 1].dir;
                return null;
            }
            holdTicks = 0;
            return null;
        }

        void EndLoop()
        {
            int loop = Session.LoopsUsed;
            if (Session.CommitLoop()) StartRewind(StartFreshLoop, loop);
        }

        void StartRewind(Action then) => StartRewind(then, Session.LoopsUsed);

        void StartRewind(Action then, int loopNumber)
        {
            ClearHeldInput();
            rewindingLoop = loopNumber;
            int n = Session.Snapshots.Count - 1;
            if (n <= 0)
            {
                then();
                return;
            }
            float seconds = config.RewindDuration(n);
            Session.BeginRewind();
            rewindPos = n;
            rewindSecondsPerSnapshot = seconds / n;
            afterRewind = then;
            RewindStarted?.Invoke(seconds);
        }

        void FinishRewind()
        {
            rewindPos = 0f;
            var then = afterRewind;
            afterRewind = null;
            then?.Invoke();
            RewindFinished?.Invoke();
        }

        void StartFreshLoop()
        {
            Session.StartLoop();
            ResetTiming();
            NotifySection();   // erasing can go back a section, restarting back to the first
            LoopReady?.Invoke(Session.LoopsUsed);
        }

        void NotifySection()
        {
            if (Session.SectionsFinished == sectionsShown) return;
            sectionsShown = Session.SectionsFinished;
            SectionChanged?.Invoke();
        }

        void ResetTiming()
        {
            accumulator = 0f;
            stepAge = 0f;
            rewindPos = 0f;
            afterRewind = null;
            ClearHeldInput();
        }

        void ClearHeldInput()
        {
            held.Clear();
            queued.Clear();
            holdTicks = 0;
        }
    }
}
