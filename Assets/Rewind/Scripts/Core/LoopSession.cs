using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    public enum LoopPhase { Ready, Playing, Rewinding, Solved }

    /// <summary>
    /// One attempt at a level: the echoes recorded so far, the loop currently being played, and the
    /// snapshots needed to rewind it. It has no notion of real time; a driver calls <see cref="Step"/> once per tick.
    /// Reaching a checkpoint closes the section behind and starts the next one with no echoes; erasing with no echoes
    /// left goes back to the section before, with all of its echoes.
    /// </summary>
    public sealed class LoopSession
    {
        sealed class Section
        {
            public int Checkpoint = -1;     // the checkpoint it starts at (index into Layout.Checkpoints), -1 for the start
            public int Index;               // the layout's section number
            public Vector2Int Start;
            public int LoopTicks;
            public WorldState StartWorld;   // crates, ground and doors as the section began; null for the first section
            public readonly List<EchoRecording> Echoes = new List<EchoRecording>();
        }

        readonly List<Section> finished = new List<Section>();   // closed behind the player, oldest first
        readonly List<WorldSnapshot> snapshots = new List<WorldSnapshot>();
        readonly List<ParadoxEvent> paradoxes = new List<ParadoxEvent>();
        readonly Func<float, int> ticksForSeconds;
        Section current;

        /// <param name="loopTicks">Ticks per loop in the level.</param>
        /// <param name="ticksForSeconds">Turns a checkpoint's loop length in seconds into ticks. Without it, every section uses <paramref name="loopTicks"/>.</param>
        public LoopSession(LevelLayout layout, int loopTicks, Func<float, int> ticksForSeconds = null)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.ticksForSeconds = ticksForSeconds;
            current = new Section { Start = layout.Start, LoopTicks = Mathf.Max(1, loopTicks) };
            StartLoop();
        }

        List<EchoRecording> echoes => current.Echoes;

        public LevelLayout Layout { get; }
        public int LoopTicks => current.LoopTicks;
        public WorldState World { get; private set; }
        public int Tick { get; private set; }
        public LoopPhase Phase { get; private set; }
        /// <summary>What the player has done so far this loop.</summary>
        public EchoRecording Recording { get; private set; }
        /// <summary>The echoes of the current section.</summary>
        public IReadOnlyList<EchoRecording> Echoes => echoes;
        public IReadOnlyList<WorldSnapshot> Snapshots => snapshots;
        /// <summary>Paradoxes that happened during the current loop.</summary>
        public IReadOnlyList<ParadoxEvent> Paradoxes => paradoxes;
        public TickReport LastReport { get; } = new TickReport();
        public bool IsTimeUp => Tick >= LoopTicks;
        /// <summary>Loops used so far in the whole level: every finished section's, plus this section's including the current loop.</summary>
        public int LoopsUsed
        {
            get
            {
                int loops = echoes.Count + 1;
                foreach (var s in finished) loops += s.Echoes.Count + 1;
                return loops;
            }
        }

        /// <summary>The layout section the player is in.</summary>
        public int CurrentSection => current.Index;
        /// <summary>The checkpoint the current section started at, or -1 for the first section.</summary>
        public int CurrentCheckpoint => current.Checkpoint;
        public int SectionsFinished => finished.Count;
        /// <summary>Loops a finished section took, the one that reached its checkpoint included.</summary>
        public int FinishedLoops(int i) => finished[i].Echoes.Count + 1;
        public int FinishedCheckpoint(int i) => finished[i].Checkpoint;

        /// <summary>Resets the world for a fresh loop with every echo of this section at its start.</summary>
        public void StartLoop()
        {
            World = current.StartWorld == null ? WorldState.Create(Layout, echoes.Count) : current.StartWorld.Respawn(echoes.Count, current.Start);
            Simulation.UpdateDoors(Layout, World);
            Tick = 0;
            Recording = new EchoRecording();
            snapshots.Clear();
            snapshots.Add(WorldSnapshot.Capture(World));
            paradoxes.Clear();
            Phase = LoopPhase.Ready;
        }

        /// <summary>Starts the clock. Time stays frozen in the Ready phase until this is called.</summary>
        public bool Begin()
        {
            if (Phase != LoopPhase.Ready) return false;
            Phase = LoopPhase.Playing;
            return true;
        }

        public TickReport Step(Vector2Int? playerDir)
        {
            if (Phase != LoopPhase.Playing) throw new InvalidOperationException($"Cannot step while {Phase}.");
            var ev = Simulation.Advance(Layout, World, echoes, Tick, playerDir, LastReport);
            Recording.Add(ev);
            Tick++;
            snapshots.Add(WorldSnapshot.Capture(World));
            paradoxes.AddRange(LastReport.Paradoxes);
            if (LastReport.ReachedExit) Phase = LoopPhase.Solved;
            else
            {
                int cp = Layout.CheckpointIndexAt(World.Player.Cell);
                if (cp >= 0 && cp != current.Checkpoint) EnterCheckpoint(cp);
            }
            return LastReport;
        }

        /// <summary>
        /// Closes the section behind (it keeps its echoes, for going back) and starts the checkpoint's section: no echoes,
        /// the world as it is now, and the checkpoint's own loop length.
        /// </summary>
        void EnterCheckpoint(int checkpoint)
        {
            var cp = Layout.Checkpoints[checkpoint];
            finished.Add(current);
            var start = World.Respawn(0, cp.Cell);
            foreach (var s in finished) start.ClosedSection[s.Index] = true;
            int ticks = cp.Seconds > 0f && ticksForSeconds != null ? ticksForSeconds(cp.Seconds) : finished[0].LoopTicks;
            current = new Section { Checkpoint = checkpoint, Index = cp.Section, Start = cp.Cell, LoopTicks = Mathf.Max(1, ticks), StartWorld = start };
            LastReport.ReachedCheckpoint = true;
            StartLoop();
        }

        /// <summary>Keeps the current loop as a new echo. Returns false if nothing has happened yet.</summary>
        public bool CommitLoop()
        {
            if (Tick == 0) return false;
            echoes.Add(Recording);
            return true;
        }

        /// <summary>Marks the session as rewinding. The world and snapshots stay as they were until <see cref="StartLoop"/>.</summary>
        public void BeginRewind() => Phase = LoopPhase.Rewinding;

        /// <summary>
        /// Drops the newest echo. With none left in this section, goes back to the section before instead, with all of its
        /// echoes, so its last loop (the one that reached the checkpoint) can be played again. Call <see cref="StartLoop"/> after.
        /// </summary>
        public bool RemoveLastEcho()
        {
            if (echoes.Count > 0)
            {
                echoes.RemoveAt(echoes.Count - 1);
                return true;
            }
            if (finished.Count == 0) return false;
            current = finished[finished.Count - 1];
            finished.RemoveAt(finished.Count - 1);
            return true;
        }

        public bool CanRemoveEcho => echoes.Count > 0 || finished.Count > 0;

        /// <summary>Drops every echo and goes back to the first section. Call <see cref="StartLoop"/> after.</summary>
        public void ClearEchoes()
        {
            if (finished.Count > 0)
            {
                current = finished[0];
                finished.Clear();
            }
            echoes.Clear();
        }

        public void SetEchoes(IEnumerable<EchoRecording> recordings)
        {
            echoes.Clear();
            echoes.AddRange(recordings);
            StartLoop();
        }
    }
}
