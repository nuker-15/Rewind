using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rewind.Core
{
    public struct PlaythroughResult
    {
        public bool Solved;
        public int LoopsUsed;
        public List<ParadoxEvent> Paradoxes;
    }

    public struct SearchResult
    {
        /// <summary>The fastest script that reaches the goal, or null if none exists within one loop.</summary>
        public string Script;
        public int StatesExplored;
        /// <summary>True when the search stopped at its state limit, so a null script is inconclusive.</summary>
        public bool Capped;
    }

    /// <summary>
    /// Design tools: replay scripted loops, and search every possible next loop for a way to a goal.
    /// A script is a string per loop: U/D/L/R move, '.' waits; the loop ends when the string runs out.
    /// </summary>
    public static class Solver
    {
        /// <summary>
        /// Plays the scripts one loop each. A loop that reaches a checkpoint ends there, and the next script is the first
        /// loop of the checkpoint's section.
        /// </summary>
        public static PlaythroughResult Play(LevelLayout layout, IReadOnlyList<string> loops, int loopTicks, Func<float, int> ticksForSeconds = null)
        {
            var session = new LoopSession(layout, loopTicks, ticksForSeconds);
            var paradoxes = new List<ParadoxEvent>();
            for (int k = 0; k < loops.Count; k++)
            {
                PlayScript(session, loops[k], paradoxes);
                if (session.Phase == LoopPhase.Solved) break;
                if (session.Phase == LoopPhase.Ready) continue;   // reached a checkpoint: already waiting in the next section
                if (k < loops.Count - 1)
                {
                    session.CommitLoop();
                    session.StartLoop();
                }
            }
            return new PlaythroughResult
            {
                Solved = session.Phase == LoopPhase.Solved,
                LoopsUsed = session.LoopsUsed,
                Paradoxes = paradoxes,
            };
        }

        /// <summary>Records each script as an echo, in order, and returns the resulting recordings.</summary>
        public static List<EchoRecording> Record(LevelLayout layout, IReadOnlyList<string> loops, int loopTicks)
        {
            var session = new LoopSession(layout, loopTicks);
            foreach (var s in loops)
            {
                PlayScript(session, s);
                session.CommitLoop();
                session.StartLoop();
            }
            return new List<EchoRecording>(session.Echoes);
        }

        // paradoxes are gathered tick by tick, as a loop that reaches a checkpoint starts the next one straight away
        static void PlayScript(LoopSession session, string script, List<ParadoxEvent> paradoxes = null)
        {
            session.Begin();
            foreach (char c in script)
            {
                if (session.Phase != LoopPhase.Playing || session.IsTimeUp) break;
                paradoxes?.AddRange(session.Step(Dir.FromScriptChar(c)).Paradoxes);
            }
        }

        public static bool ReachedExit(LevelLayout layout, WorldState w) => layout.TileAt(w.Player.Cell).Kind == TileKind.Exit;

        public static bool AllEchoesAlive(WorldState w)
        {
            foreach (var b in w.Bodies) if (b.Kind == BodyKind.Echo && !b.Alive) return false;
            return true;
        }

        static readonly Vector2Int?[] Actions = { null, Dir.Up, Dir.Down, Dir.Left, Dir.Right };

        /// <summary>Breadth-first search over every possible next loop, given the echoes already recorded.</summary>
        public static SearchResult FindLoop(LevelLayout layout, IReadOnlyList<EchoRecording> echoes, int loopTicks,
            Func<WorldState, bool> goal, int maxStates = 3_000_000)
        {
            var start = WorldState.Create(layout, echoes.Count);
            Simulation.UpdateDoors(layout, start);
            var layer = new List<(WorldState world, string path)> { (start, string.Empty) };
            var key = new StringBuilder(64);
            int explored = 0;
            for (int t = 0; t < loopTicks && layer.Count > 0; t++)
            {
                var seen = new HashSet<string>();
                var next = new List<(WorldState, string)>();
                foreach (var (world, path) in layer)
                {
                    foreach (var action in Actions)
                    {
                        var w = world.Clone();
                        Simulation.Advance(layout, w, echoes, t, action);
                        var newPath = path + Dir.ToScriptChar(action);
                        if (goal(w)) return new SearchResult { Script = newPath, StatesExplored = explored };
                        if (!seen.Add(StateKey(layout, w, key))) continue;
                        next.Add((w, newPath));
                        if (++explored > maxStates) return new SearchResult { StatesExplored = explored, Capped = true };
                    }
                }
                layer = next;
            }
            return new SearchResult { StatesExplored = explored };
        }

        // Echo positions are fully determined by the tick and whether they are still alive.
        static string StateKey(LevelLayout layout, WorldState w, StringBuilder sb)
        {
            sb.Clear();
            sb.Append(w.Player.Cell.x).Append(',').Append(w.Player.Cell.y).Append('|');
            foreach (var c in w.Crates)
            {
                if (c.Sunk) sb.Append('s');
                else sb.Append(c.Cell.x).Append(',').Append(c.Cell.y);
                sb.Append(';');
            }
            foreach (var b in w.Bodies) if (b.Kind == BodyKind.Echo) sb.Append(b.Alive ? '1' : '0');
            sb.Append('|');
            foreach (var o in w.DoorOpen) sb.Append(o ? '1' : '0');
            sb.Append('|');
            foreach (int i in layout.DynamicCells) sb.Append((int)w.Ground[i]);
            return sb.ToString();
        }
    }
}
