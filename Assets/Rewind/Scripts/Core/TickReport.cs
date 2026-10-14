using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>Everything noteworthy that happened during one tick, for views, effects and sound.</summary>
    public sealed class TickReport
    {
        public int Tick;
        public StepEvent PlayerStep;
        public bool PlayerBumped;
        public int CratesPushed;
        public bool DoorsOpened;
        public bool DoorsClosed;
        public bool ReachedExit;
        /// <summary>The player stepped onto a checkpoint: the session has moved on to its section and waits in Ready.</summary>
        public bool ReachedCheckpoint;
        public readonly List<Vector2Int> Crumbled = new List<Vector2Int>();
        public readonly List<Vector2Int> Filled = new List<Vector2Int>();
        public readonly List<ParadoxEvent> Paradoxes = new List<ParadoxEvent>();

        internal bool[] DoorBuffer = System.Array.Empty<bool>();

        public void Begin(int tick)
        {
            Tick = tick;
            PlayerStep = StepEvent.Wait;
            PlayerBumped = false;
            CratesPushed = 0;
            DoorsOpened = false;
            DoorsClosed = false;
            ReachedExit = false;
            ReachedCheckpoint = false;
            Crumbled.Clear();
            Filled.Clear();
            Paradoxes.Clear();
        }
    }
}
