using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>A level made in the in-game editor.</summary>
    [Serializable]
    public sealed class CustomLevel
    {
        public string id;
        public string name = "Untitled";
        public int par = 2;
        public string map;
        /// <summary>Fewest loops this level has been solved in while testing, or 0 if never.</summary>
        public int bestLoops;
        public long updatedTicks;
    }

    /// <summary>
    /// Keeps the player's own levels on this device in PlayerPrefs (IndexedDB on WebGL), so the editor works in builds.
    /// </summary>
    public static class CustomLevelStore
    {
        const string Key = "rewind.customLevels.v1";
        const string LastKey = "rewind.customLevels.last";

        [Serializable]
        sealed class Root
        {
            public List<CustomLevel> levels = new List<CustomLevel>();
        }

        static Root cache;

        public static IReadOnlyList<CustomLevel> All
        {
            get
            {
                Load();
                return cache.levels;
            }
        }

        /// <summary>The level open in the editor last time, so it can be resumed.</summary>
        public static string LastEditedId
        {
            get => PlayerPrefs.GetString(LastKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(LastKey, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        public static CustomLevel Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Load();
            return cache.levels.Find(l => l.id == id);
        }

        /// <summary>Adds or updates a level. A level without an id gets one.</summary>
        public static void Save(CustomLevel level)
        {
            if (level == null) return;
            Load();
            if (string.IsNullOrEmpty(level.id)) level.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            level.updatedTicks = DateTime.UtcNow.Ticks;
            int index = cache.levels.FindIndex(l => l.id == level.id);
            if (index >= 0) cache.levels[index] = level;
            else cache.levels.Add(level);
            Write();
        }

        public static void Delete(string id)
        {
            Load();
            if (cache.levels.RemoveAll(l => l.id == id) > 0) Write();
        }

        static void Load()
        {
            if (cache != null) return;
            string json = PlayerPrefs.GetString(Key, string.Empty);
            try { cache = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Root>(json); }
            catch (ArgumentException) { cache = null; }
            cache ??= new Root();
            cache.levels ??= new List<CustomLevel>();
        }

        static void Write()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(cache));
            PlayerPrefs.Save();
        }
    }
}
