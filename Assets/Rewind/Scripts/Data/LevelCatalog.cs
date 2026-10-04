using System.Collections.Generic;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>The ordered list of tapes the player can pick from.</summary>
    [CreateAssetMenu(menuName = "Rewind/Level Catalog", fileName = "LevelCatalog", order = 1)]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] List<LevelData> levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels.Count;
        public LevelData this[int index] => levels[index];
        public int IndexOf(LevelData level) => levels.IndexOf(level);
    }
}
