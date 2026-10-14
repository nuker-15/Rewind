using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    public enum BodyKind : byte { Player, Echo }

    public sealed class Body
    {
        public BodyKind Kind;
        public int EchoIndex;            // -1 for the player
        public Vector2Int Cell;
        public Vector2Int PrevCell;      // where it was at the start of the last tick
        public Vector2Int Facing = Dir.Down;
        public bool Alive = true;        // echoes only: false once it has hit a paradox
        public int DeadAtTick = -1;
        public ParadoxReason Reason;

        public bool IsLiving => Kind == BodyKind.Player || Alive;
        public Body Clone() => (Body)MemberwiseClone();
    }

    public sealed class Crate
    {
        public int Id;
        public Vector2Int Cell;
        public Vector2Int PrevCell;
        public bool Sunk;                // fell into a hole and now fills it

        public Crate Clone() => (Crate)MemberwiseClone();
    }

    /// <summary>Everything that changes during a loop. Echo bodies come first in recording order; the player is last.</summary>
    public sealed class WorldState
    {
        public readonly List<Body> Bodies;
        public readonly List<Crate> Crates;
        public readonly bool[] DoorOpen;
        public readonly GroundState[] Ground;
        /// <summary>Sections behind the player's checkpoints. Nothing can move into them.</summary>
        public readonly bool[] ClosedSection;

        public Body Player => Bodies[Bodies.Count - 1];
        public int EchoCount => Bodies.Count - 1;

        WorldState(List<Body> bodies, List<Crate> crates, bool[] doorOpen, GroundState[] ground, bool[] closedSection)
        {
            Bodies = bodies;
            Crates = crates;
            DoorOpen = doorOpen;
            Ground = ground;
            ClosedSection = closedSection;
        }

        public static WorldState Create(LevelLayout layout, int echoCount)
        {
            var crates = new List<Crate>(layout.CrateStarts.Count);
            for (int i = 0; i < layout.CrateStarts.Count; i++)
                crates.Add(new Crate { Id = i, Cell = layout.CrateStarts[i], PrevCell = layout.CrateStarts[i] });

            var ground = new GroundState[layout.CellCount];
            for (int i = 0; i < ground.Length; i++) ground[i] = layout.InitialGround(i);

            return new WorldState(Spawn(echoCount, layout.Start), crates, new bool[layout.Doors.Count], ground, new bool[layout.SectionCount]);
        }

        /// <summary>A copy of this world with fresh bodies: every echo and the player at <paramref name="start"/>.</summary>
        public WorldState Respawn(int echoCount, Vector2Int start)
        {
            var crates = new List<Crate>(Crates.Count);
            foreach (var c in Crates) crates.Add(c.Clone());
            return new WorldState(Spawn(echoCount, start), crates, (bool[])DoorOpen.Clone(), (GroundState[])Ground.Clone(), (bool[])ClosedSection.Clone());
        }

        static List<Body> Spawn(int echoCount, Vector2Int start)
        {
            var bodies = new List<Body>(echoCount + 1);
            for (int i = 0; i < echoCount; i++)
                bodies.Add(new Body { Kind = BodyKind.Echo, EchoIndex = i, Cell = start, PrevCell = start });
            bodies.Add(new Body { Kind = BodyKind.Player, EchoIndex = -1, Cell = start, PrevCell = start });
            return bodies;
        }

        public WorldState Clone()
        {
            var bodies = new List<Body>(Bodies.Count);
            foreach (var b in Bodies) bodies.Add(b.Clone());
            var crates = new List<Crate>(Crates.Count);
            foreach (var c in Crates) crates.Add(c.Clone());
            return new WorldState(bodies, crates, (bool[])DoorOpen.Clone(), (GroundState[])Ground.Clone(), ClosedSection);   // closed sections never change mid-loop
        }

        public Crate CrateAt(Vector2Int cell)
        {
            for (int i = 0; i < Crates.Count; i++)
            {
                var c = Crates[i];
                if (!c.Sunk && c.Cell == cell) return c;
            }
            return null;
        }

        public bool AnyBodyAt(Vector2Int cell)
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                var b = Bodies[i];
                if (b.IsLiving && b.Cell == cell) return true;
            }
            return false;
        }
    }

    /// <summary>A frozen copy of the world after one tick, used to play the loop back during the rewind.</summary>
    public sealed class WorldSnapshot
    {
        public readonly Vector2Int[] BodyCells;
        public readonly Vector2Int[] BodyFacing;
        public readonly bool[] BodyLiving;
        public readonly Vector2Int[] CrateCells;
        public readonly bool[] CrateSunk;
        public readonly bool[] DoorOpen;
        public readonly GroundState[] Ground;

        WorldSnapshot(WorldState w)
        {
            int n = w.Bodies.Count;
            BodyCells = new Vector2Int[n];
            BodyFacing = new Vector2Int[n];
            BodyLiving = new bool[n];
            for (int i = 0; i < n; i++)
            {
                BodyCells[i] = w.Bodies[i].Cell;
                BodyFacing[i] = w.Bodies[i].Facing;
                BodyLiving[i] = w.Bodies[i].IsLiving;
            }
            CrateCells = new Vector2Int[w.Crates.Count];
            CrateSunk = new bool[w.Crates.Count];
            for (int i = 0; i < w.Crates.Count; i++)
            {
                CrateCells[i] = w.Crates[i].Cell;
                CrateSunk[i] = w.Crates[i].Sunk;
            }
            DoorOpen = (bool[])w.DoorOpen.Clone();
            Ground = (GroundState[])w.Ground.Clone();
        }

        public static WorldSnapshot Capture(WorldState w) => new WorldSnapshot(w);
    }
}
