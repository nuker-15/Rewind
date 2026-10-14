using System;
using System.Text;
using Rewind.Core;
using Rewind.Data;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    /// <summary>
    /// Checks levels the way a designer would: the map parses, the reference solution still solves it at par
    /// without paradoxes, and there is no way to finish in a single loop.
    /// </summary>
    public static class LevelValidator
    {
        public const int OneLoopSearchLimit = 300_000;

        [MenuItem("Rewind/Validate All Levels", priority = 20)]
        public static void ValidateAll()
        {
            int failures = 0, count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LevelData"))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!level) continue;
                count++;
                bool ok = Validate(level, FindLoopTicks(level), true, out var report);
                if (ok) Debug.Log($"[Rewind] {level.name}: OK. {report}", level);
                else { failures++; Debug.LogError($"[Rewind] {level.name}: {report}", level); }
            }
            Debug.Log($"[Rewind] Validated {count} levels, {failures} with problems.");
        }

        /// <summary>Ticks in one loop of this level, from its own loop length or the game config's default.</summary>
        public static int FindLoopTicks(LevelData level)
        {
            var config = FindConfig();
            return config ? config.LoopTicksFor(level ? level.LoopSeconds : 0f) : 80;
        }

        public static GameConfig FindConfig()
        {
            var guids = AssetDatabase.FindAssets("t:GameConfig");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static bool Validate(LevelData level, int loopTicks, bool searchOneLoop, out string report)
        {
            var sb = new StringBuilder();
            LevelLayout layout;
            try { layout = level.BuildLayout(); }
            catch (FormatException e)
            {
                report = "Map error: " + e.Message;
                return false;
            }

            bool ok = true;
            if (level.ReferenceSolution.Count == 0)
            {
                sb.Append("No reference solution to check. ");
            }
            else
            {
                var config = FindConfig();
                var result = Solver.Play(layout, level.ReferenceSolution, loopTicks, config ? config.LoopTicksFor : (Func<float, int>)null);
                if (!result.Solved) { ok = false; sb.Append("Reference solution no longer reaches the exit. "); }
                else if (result.LoopsUsed != level.TotalPar) { ok = false; sb.Append($"Reference solution takes {result.LoopsUsed} loops but par is {level.TotalPar}. "); }
                else sb.Append($"Reference solution solves it in {result.LoopsUsed} loops. ");
                if (result.Paradoxes.Count > 0)
                {
                    ok = false;
                    sb.Append($"It causes {result.Paradoxes.Count} paradox(es), first: echo {result.Paradoxes[0].EchoIndex + 1} at tick {result.Paradoxes[0].Tick}. ");
                }
            }

            if (searchOneLoop)
            {
                var search = Solver.FindLoop(layout, Array.Empty<EchoRecording>(), loopTicks, w => Solver.ReachedExit(layout, w), OneLoopSearchLimit);
                if (search.Script != null) { ok = false; sb.Append($"Can be finished in ONE loop: {search.Script}. "); }
                else if (search.Capped) sb.Append($"One-loop search stopped after {search.StatesExplored:N0} states (inconclusive). ");
                else sb.Append("Cannot be finished in one loop. ");
            }
            report = sb.ToString().Trim();
            return ok;
        }
    }
}
