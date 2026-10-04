using System;

namespace Rewind.UI
{
    /// <summary>Hooks for UI feedback such as sound, so widgets do not need references to an audio player.</summary>
    public static class UIEvents
    {
        public static event Action Navigated;
        public static event Action Activated;

        public static void RaiseNavigated() => Navigated?.Invoke();
        public static void RaiseActivated() => Activated?.Invoke();
    }
}
