using System;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>Best loop counts per level, kept in PlayerPrefs under the level asset's name.</summary>
    public static class PlayerProgress
    {
        const string Prefix = "rewind.best.";

        public static event Action Changed;

        public static int GetBest(LevelData level) => level ? PlayerPrefs.GetInt(Prefix + level.Id, 0) : 0;
        public static bool IsSolved(LevelData level) => GetBest(level) > 0;

        /// <summary>Stores a finished run. Returns true if it beat the previous best.</summary>
        public static bool Record(LevelData level, int loops)
        {
            if (!level || loops <= 0) return false;
            int best = GetBest(level);
            if (best > 0 && best <= loops) return false;
            PlayerPrefs.SetInt(Prefix + level.Id, loops);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        public static void Clear(LevelCatalog catalog)
        {
            if (!catalog) return;
            foreach (var level in catalog.Levels) if (level) PlayerPrefs.DeleteKey(Prefix + level.Id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Player-facing settings shared by gameplay and UI.</summary>
    public static class GameSettings
    {
        const string MuteKey = "rewind.muted";

        public static event Action Changed;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Apply();
                Changed?.Invoke();
            }
        }

        public static void Apply() => AudioListener.volume = Muted ? 0f : 1f;

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Screen.fullScreen = value;
                Changed?.Invoke();
            }
        }
    }
}
