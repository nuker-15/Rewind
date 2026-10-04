using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    public enum LoopPhase { Ready, Playing, Rewinding, Solved }

    /// <summary>
    /// One attempt at a level: the echoes recorded so far, the loop currently being played, and the
    /// snapshots needed to rewind it. It has no notion of real time; a driver calls <see cref="Step"/> once per tick.
    /// </summary>
    public sealed class LoopSession
    {
        readonly List<EchoRecording> echoes = new List<EchoRecording>();
        readonly List<WorldSnapshot> snapshots = new List<WorldSnapshot>();
        readonly List<ParadoxEvent> paradoxes = new List<ParadoxEvent>();

        public LoopSession(LevelLayout layout, int loopTicks)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            LoopTicks = Mathf.Max(1, loopTicks);
            StartLoop();
        }

        public LevelLayout Layout { get; }
        public int LoopTicks { get; }
        public WorldState World { get; private set; }
        public int Tick { get; private set; }
        public LoopPhase Phase { get; private set; }
        /// <summary>What the player has done so far this loop.</summary>
        public EchoRecording Recording { get; private set; }
        public IReadOnlyList<EchoRecording> Echoes => echoes;
        public IReadOnlyList<WorldSnapshot> Snapshots => snapshots;
        /// <summary>Paradoxes that happened during the current loop.</summary>
        public IReadOnlyList<ParadoxEvent> Paradoxes => paradoxes;
        public TickReport LastReport { get; } = new TickReport();
        public bool IsTimeUp => Tick >= LoopTicks;
        public int LoopsUsed => echoes.Count + 1;

        /// <summary>Resets the world for a fresh loop with every recorded echo at the start.</summary>
        public void StartLoop()
        {
            World = WorldState.Create(Layout, echoes.Count);
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
            return LastReport;
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

        public bool RemoveLastEcho()
        {
            if (echoes.Count == 0) return false;
            echoes.RemoveAt(echoes.Count - 1);
            return true;
        }

        public void ClearEchoes() => echoes.Clear();

        public void SetEchoes(IEnumerable<EchoRecording> recordings)
        {
            echoes.Clear();
            echoes.AddRange(recordings);
            StartLoop();
        }
    }
}
