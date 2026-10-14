using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>
    /// The rules of one tick. Echoes act first, in recording order, and each must reproduce the outcome it
    /// recorded or it vanishes. The live player acts last, exactly as it will when it becomes the newest echo.
    /// </summary>
    public static class Simulation
    {
        enum Outcome : byte { Move, Push, Blocked }
        enum Block : byte { None, Wall, Door, Pit, CrateWall, CrateDoor, CrateCrate, CrateBody }

        struct Attempt
        {
            public Outcome Outcome;
            public Block Block;
            public Crate Crate;
            public Vector2Int Target;
            public Vector2Int CrateTarget;
        }

        /// <summary>Advances the world by one tick. Returns what the player did (a wait if it did not move).</summary>
        public static StepEvent Advance(LevelLayout layout, WorldState world, IReadOnlyList<EchoRecording> echoes,
            int tick, Vector2Int? playerDir, TickReport report = null)
        {
            report?.Begin(tick);
            if (report != null)
            {
                if (report.DoorBuffer.Length != world.DoorOpen.Length) report.DoorBuffer = new bool[world.DoorOpen.Length];
                System.Array.Copy(world.DoorOpen, report.DoorBuffer, world.DoorOpen.Length);
            }

            foreach (var b in world.Bodies) b.PrevCell = b.Cell;
            foreach (var c in world.Crates) c.PrevCell = c.Cell;

            for (int i = 0; i < world.Bodies.Count; i++)
            {
                var echo = world.Bodies[i];
                if (echo.Kind != BodyKind.Echo || !echo.Alive) continue;
                var ev = echoes[echo.EchoIndex][tick];
                if (ev.IsWait) continue;
                var a = TryStep(layout, world, echo, ev.Dir);
                var expected = ev.Kind == StepKind.Push ? Outcome.Push : Outcome.Move;
                if (a.Outcome != expected)
                {
                    var reason = Explain(ev, a);
                    echo.Alive = false;
                    echo.DeadAtTick = tick;
                    echo.Reason = reason;
                    report?.Paradoxes.Add(new ParadoxEvent(echo.EchoIndex, tick, echo.Cell, ev.Dir, reason));
                    continue;
                }
                Apply(layout, world, echo, a, ev.Dir, report);
            }

            var player = world.Player;
            var result = StepEvent.Wait;
            if (playerDir.HasValue)
            {
                var d = playerDir.Value;
                player.Facing = d;
                var a = TryStep(layout, world, player, d);
                if (a.Outcome != Outcome.Blocked)
                {
                    Apply(layout, world, player, a, d, report);
                    result = new StepEvent(a.Outcome == Outcome.Push ? StepKind.Push : StepKind.Move, d);
                }
                else if (report != null) report.PlayerBumped = true;
            }

            UpdateGround(layout, world, report);
            UpdateDoors(layout, world);

            if (report != null)
            {
                report.PlayerStep = result;
                report.ReachedExit = layout.TileAt(player.Cell).Kind == TileKind.Exit;
                for (int i = 0; i < world.DoorOpen.Length; i++)
                {
                    if (world.DoorOpen[i] && !report.DoorBuffer[i]) report.DoorsOpened = true;
                    if (!world.DoorOpen[i] && report.DoorBuffer[i]) report.DoorsClosed = true;
                }
            }
            return result;
        }

        static Attempt TryStep(LevelLayout layout, WorldState world, Body body, Vector2Int d)
        {
            var target = body.Cell + d;
            var a = new Attempt { Target = target };
            var t = layout.TileAt(target);
            if (t.Kind == TileKind.Wall || IsClosed(layout, world, target)) return Blocked(a, Block.Wall);
            if (t.Kind == TileKind.Door && !DoorOpenAt(layout, world, target)) return Blocked(a, Block.Door);
            if (world.Ground[layout.Index(target)] == GroundState.Hole) return Blocked(a, Block.Pit);

            var crate = world.CrateAt(target);
            if (crate != null)
            {
                var dest = target + d;
                var dt = layout.TileAt(dest);
                // crates stay off exits and checkpoints, so neither can ever be blocked
                if (dt.Kind == TileKind.Wall || dt.Kind == TileKind.Exit || dt.Kind == TileKind.Checkpoint || IsClosed(layout, world, dest))
                    return Blocked(a, Block.CrateWall);
                if (dt.Kind == TileKind.Door && !DoorOpenAt(layout, world, dest)) return Blocked(a, Block.CrateDoor);
                if (world.Ground[layout.Index(dest)] != GroundState.Hole)
                {
                    if (world.CrateAt(dest) != null) return Blocked(a, Block.CrateCrate);
                    if (world.AnyBodyAt(dest)) return Blocked(a, Block.CrateBody);
                }
                a.Outcome = Outcome.Push;
                a.Crate = crate;
                a.CrateTarget = dest;
                return a;
            }
            a.Outcome = Outcome.Move;
            return a;
        }

        static Attempt Blocked(Attempt a, Block why)
        {
            a.Outcome = Outcome.Blocked;
            a.Block = why;
            return a;
        }

        static void Apply(LevelLayout layout, WorldState world, Body body, Attempt a, Vector2Int d, TickReport report)
        {
            if (a.Outcome == Outcome.Push)
            {
                a.Crate.Cell = a.CrateTarget;
                int i = layout.Index(a.CrateTarget);
                if (world.Ground[i] == GroundState.Hole)
                {
                    a.Crate.Sunk = true;
                    world.Ground[i] = GroundState.Filled;
                    report?.Filled.Add(a.CrateTarget);
                }
                else if (report != null) report.CratesPushed++;
            }
            body.Cell = a.Target;
            body.Facing = d;
        }

        static ParadoxReason Explain(StepEvent ev, Attempt a)
        {
            if (a.Block == Block.Door) return ParadoxReason.DoorShut;
            if (a.Block == Block.Pit) return ParadoxReason.FloorCrumbled;
            if (ev.Kind == StepKind.Push)
            {
                if (a.Outcome == Outcome.Move) return ParadoxReason.CrateMissing;
                switch (a.Block)
                {
                    case Block.CrateBody: return ParadoxReason.CrateBlockedByBody;
                    case Block.CrateDoor: return ParadoxReason.CrateBlockedByDoor;
                    case Block.CrateCrate: return ParadoxReason.CrateBlockedByCrate;
                    default: return ParadoxReason.CrateBlocked;
                }
            }
            if (a.Outcome == Outcome.Push || a.Block == Block.CrateWall || a.Block == Block.CrateDoor ||
                a.Block == Block.CrateCrate || a.Block == Block.CrateBody)
                return ParadoxReason.CrateInPath;
            return ParadoxReason.Unknown;
        }

        static bool IsClosed(LevelLayout layout, WorldState world, Vector2Int cell)
        {
            int s = layout.SectionOf(cell);
            return s >= 0 && world.ClosedSection[s];
        }

        static bool DoorOpenAt(LevelLayout layout, WorldState world, Vector2Int cell)
        {
            int i = layout.DoorIndexAt(cell);
            return i >= 0 && world.DoorOpen[i];
        }

        /// <summary>Cracked floor remembers being stood on, and gives way the first tick it is empty again.</summary>
        static void UpdateGround(LevelLayout layout, WorldState world, TickReport report)
        {
            foreach (int i in layout.CrackCells)
            {
                var g = world.Ground[i];
                if (g != GroundState.Cracked && g != GroundState.Stepped) continue;
                var cell = layout.CellOf(i);
                bool occupied = world.AnyBodyAt(cell);
                if (g == GroundState.Cracked && occupied) world.Ground[i] = GroundState.Stepped;
                else if (g == GroundState.Stepped && !occupied)
                {
                    var crate = world.CrateAt(cell);
                    if (crate != null)
                    {
                        crate.Sunk = true;
                        world.Ground[i] = GroundState.Filled;
                    }
                    else world.Ground[i] = GroundState.Hole;
                    report?.Crumbled.Add(cell);
                }
            }
        }

        /// <summary>A door counts as held only while every plate linked to it is pressed. Doors stay open while occupied.</summary>
        public static void UpdateDoors(LevelLayout layout, WorldState world)
        {
            var plates = layout.Plates;
            Span<bool> pressed = plates.Count <= 256 ? stackalloc bool[plates.Count] : new bool[plates.Count];
            for (int i = 0; i < plates.Count; i++)
                pressed[i] = world.CrateAt(plates[i].Cell) != null || world.AnyBodyAt(plates[i].Cell);

            for (int i = 0; i < layout.Doors.Count; i++)
            {
                var d = layout.Doors[i];
                var linked = layout.DoorPlates(i);
                bool held = true;   // a door with no plates counts as held, as a colour with no plates always did
                for (int k = 0; k < linked.Count; k++) held &= pressed[linked[k]];
                bool wantOpen = held != d.Inverse;
                bool jammed = world.DoorOpen[i] && (world.AnyBodyAt(d.Cell) || world.CrateAt(d.Cell) != null);
                world.DoorOpen[i] = wantOpen || jammed;
            }
        }
    }
}
