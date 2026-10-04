using System.Collections.Generic;
using Rewind.Core;
using UnityEngine;

namespace Rewind.Data
{
    /// <summary>One puzzle. The map is plain text so levels can be written and diffed easily.</summary>
    [CreateAssetMenu(menuName = "Rewind/Level", fileName = "Level", order = 0)]
    public sealed class LevelData : ScriptableObject
    {
        public const string MapLegend =
            "#  wall            S  start          X  exit\n" +
            "o  crate           ~  cracked floor  _  hole\n" +
            "1 2 3  plates (amber, cyan, orange)\n" +
            "a b c  doors that open while every plate of their colour is held\n" +
            "A B C  doors that shut while every plate of their colour is held\n" +
            "anything else  floor";

        [SerializeField] string displayName = "Untitled";
        [SerializeField, Min(1)] int par = 2;
        [SerializeField, TextArea(2, 6)] string hint = "";
        [SerializeField, TextArea(6, 30), Tooltip(MapLegend)] string map = "#####\n#S.X#\n#####";
        [SerializeField, Tooltip("A known solution, one script per loop: U/D/L/R move, '.' waits. Used by the level validator and tests.")]
        List<string> referenceSolution = new List<string>();

        public string DisplayName => displayName;
        public int Par => par;
        public string Hint => hint;
        public string Map => map;
        public IReadOnlyList<string> ReferenceSolution => referenceSolution;
        /// <summary>Stable key for saved progress.</summary>
        public string Id => name;

        public LevelLayout BuildLayout() => LevelLayout.Parse(map);
    }
}
