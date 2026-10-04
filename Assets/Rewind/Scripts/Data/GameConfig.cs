using UnityEngine;

namespace Rewind.Data
{
    /// <summary>Timing and feel. Changing the tick or loop length changes the puzzles, so re-run the level validator after.</summary>
    [CreateAssetMenu(menuName = "Rewind/Game Config", fileName = "GameConfig", order = 2)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Time loop")]
        [SerializeField, Min(0.02f), Tooltip("Seconds per grid step.")] float tickSeconds = 0.125f;
        [SerializeField, Min(1f), Tooltip("Length of every loop in seconds.")] float loopSeconds = 10f;

        [Header("Input")]
        [SerializeField, Min(1), Tooltip("Ticks a direction must be held before it starts repeating.")] int repeatDelayTicks = 2;
        [SerializeField, Min(1), Tooltip("How many taps can be buffered ahead of the clock.")] int inputQueueLimit = 3;

        [Header("Presentation")]
        [SerializeField, Min(0f), Tooltip("Seconds a body takes to slide one tile.")] float moveAnimSeconds = 0.1f;
        [SerializeField, Min(0.01f), Tooltip("Rewind playback speed relative to the loop.")] float rewindSpeed = 5f;
        [SerializeField, Min(0f)] float minRewindSeconds = 0.5f;
        [SerializeField, Min(0f)] float maxRewindSeconds = 1.8f;
        [SerializeField, Min(0), Tooltip("How many ticks ahead each echo's dotted trail shows.")] int previewTicks = 16;
        [SerializeField, Min(0f), Tooltip("Seconds left in a loop when the warning beeps start.")] float warningSeconds = 3f;

        public float TickSeconds => tickSeconds;
        public float LoopSeconds => loopSeconds;
        public int LoopTicks => Mathf.Max(1, Mathf.RoundToInt(loopSeconds / tickSeconds));
        public int TicksPerSecond => Mathf.Max(1, Mathf.RoundToInt(1f / tickSeconds));
        public int RepeatDelayTicks => repeatDelayTicks;
        public int InputQueueLimit => inputQueueLimit;
        public float MoveAnimSeconds => moveAnimSeconds;
        public int PreviewTicks => previewTicks;
        public int WarningTicks => Mathf.RoundToInt(warningSeconds / tickSeconds);

        public float RewindDuration(int ticks) =>
            Mathf.Clamp(ticks * tickSeconds / rewindSpeed, minRewindSeconds, maxRewindSeconds);
    }
}
