using UnityEngine;

namespace Rewind.Core
{
    /// <summary>Why an echo could not repeat its history.</summary>
    public enum ParadoxReason : byte
    {
        None,
        DoorShut,
        FloorCrumbled,
        CrateMissing,
        CrateBlockedByBody,
        CrateBlockedByDoor,
        CrateBlockedByCrate,
        CrateBlocked,
        CrateInPath,
        Unknown,
    }

    public readonly struct ParadoxEvent
    {
        public readonly int EchoIndex;
        public readonly int Tick;
        public readonly Vector2Int Cell;
        public readonly Vector2Int AttemptedDir;
        public readonly ParadoxReason Reason;

        public ParadoxEvent(int echoIndex, int tick, Vector2Int cell, Vector2Int attemptedDir, ParadoxReason reason)
        {
            EchoIndex = echoIndex;
            Tick = tick;
            Cell = cell;
            AttemptedDir = attemptedDir;
            Reason = reason;
        }
    }

    public static class ParadoxText
    {
        public static string Describe(ParadoxReason reason)
        {
            switch (reason)
            {
                case ParadoxReason.DoorShut: return "Walked into a door that was shut.";
                case ParadoxReason.FloorCrumbled: return "Stepped onto floor that had already crumbled.";
                case ParadoxReason.CrateMissing: return "Reached for a crate that was no longer there.";
                case ParadoxReason.CrateBlockedByBody: return "Could not push its crate: someone stood in the way.";
                case ParadoxReason.CrateBlockedByDoor: return "Could not push its crate through a shut door.";
                case ParadoxReason.CrateBlockedByCrate: return "Could not push its crate: another crate was in the way.";
                case ParadoxReason.CrateBlocked: return "Could not push its crate.";
                case ParadoxReason.CrateInPath: return "Found a crate blocking its path.";
                case ParadoxReason.None: return string.Empty;
                default: return "Could not repeat its history.";
            }
        }
    }
}
