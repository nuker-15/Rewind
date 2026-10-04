using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rewind.Core
{
    /// <summary>What one body actually did during one tick. Failed moves are recorded as waits.</summary>
    public readonly struct StepEvent
    {
        public readonly StepKind Kind;
        public readonly Vector2Int Dir;

        public StepEvent(StepKind kind, Vector2Int dir)
        {
            Kind = kind;
            Dir = dir;
        }

        public static readonly StepEvent Wait = new StepEvent(StepKind.Wait, Vector2Int.zero);
        public bool IsWait => Kind == StepKind.Wait;
    }

    /// <summary>One past loop, tick by tick. Ticks past the end of the recording count as waiting.</summary>
    public sealed class EchoRecording
    {
        readonly List<StepEvent> steps = new List<StepEvent>();

        public int Length => steps.Count;
        public StepEvent this[int tick] => tick >= 0 && tick < steps.Count ? steps[tick] : StepEvent.Wait;
        public IReadOnlyList<StepEvent> Steps => steps;

        public void Add(StepEvent step) => steps.Add(step);

        public EchoRecording Clone()
        {
            var copy = new EchoRecording();
            copy.steps.AddRange(steps);
            return copy;
        }

        /// <summary>The recording as a script string (U/D/L/R moves, '.' waits).</summary>
        public string ToScript()
        {
            var sb = new StringBuilder(steps.Count);
            foreach (var s in steps) sb.Append(s.IsWait ? '.' : Dir.ToScriptChar(s.Dir));
            return sb.ToString();
        }
    }
}
