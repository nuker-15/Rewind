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
            "p  plate\n" +
            "d  door that opens while every plate linked to it is held\n" +
            "D  door that shuts while every plate linked to it is held\n" +
            "+  checkpoint: a new spawn and a fresh timer; the part behind it closes\n" +
            "Older maps: 1 2 3 plates, a b c / A B C doors, linked by number\n" +
            "anything else  floor";

        public const float MinLoopSeconds = 5f, MaxLoopSeconds = 120f;

        [SerializeField] string displayName = "Untitled";
        [SerializeField, Min(1)] int par = 2;
        [SerializeField, Range(0f, MaxLoopSeconds), Tooltip("Seconds per loop. 0 uses the default from the game config.")] float loopSeconds;
        [SerializeField, TextArea(2, 6)] string hint = "";
        [SerializeField, TextArea(6, 30), Tooltip(MapLegend)] string map = "#####\n#S.X#\n#####";
        [SerializeField, Tooltip("Which plates control which doors: \"x,y>x,y\" pairs (a plate, then a door) separated by ';'. The level editor's LINK tool writes these.")]
        string links = "";
        [SerializeField, Tooltip("Checkpoint (+) settings: \"x,y,par,seconds\" entries separated by ';'. Seconds of 0 use the level's loop length; a checkpoint without an entry has par 1.")]
        string checkpoints = "";
        [SerializeField, Tooltip("Rooms the camera frames one at a time: \"x,y,w,h,s\" entries (walls included; s slides to the room, c cuts) separated by ';'. Empty makes the whole map one room. The level editor's ROOM tool writes these.")]
        string rooms = "";
        [SerializeField, Tooltip("A known solution, one script per loop: U/D/L/R move, '.' waits. Used by the level validator and tests.")]
        List<string> referenceSolution = new List<string>();

        public string DisplayName => displayName;
        public int Par => par;
        /// <summary>Seconds per loop, or 0 for the game's default.</summary>
        public float LoopSeconds => loopSeconds;
        public string Hint => hint;
        public string Map => map;
        public string Links => links;
        public string Checkpoints => checkpoints;
        public string Rooms => rooms;

        /// <summary>Par for the whole level: the first section's, plus each checkpoint's on the way to the exit.</summary>
        public int TotalPar
        {
            get
            {
                try { return BuildLayout().TotalPar(par); }
                catch (System.FormatException) { return par; }
            }
        }
        public IReadOnlyList<string> ReferenceSolution => referenceSolution;
        /// <summary>Stable key for saved progress.</summary>
        public string Id => name;

        public LevelLayout BuildLayout() => LevelLayout.Parse(map, links, checkpoints);

        /// <summary>The level's rooms; never empty, as a level without rooms is one room.</summary>
        public List<Room> RoomsFor(LevelLayout layout) => LevelRooms.For(rooms, layout.Width, layout.Height);

        /// <summary>A level that exists only in memory, such as one made in the in-game editor. Never saved as an asset.</summary>
        public static LevelData CreateRuntime(CustomLevel source)
        {
            var level = CreateInstance<LevelData>();
            level.hideFlags = HideFlags.DontSave;
            level.Configure(source);
            return level;
        }

        /// <summary>Replaces the contents of a level made with <see cref="CreateRuntime"/>.</summary>
        public void Configure(CustomLevel source)
        {
            displayName = string.IsNullOrWhiteSpace(source.name) ? "Untitled" : source.name.Trim();
            par = Mathf.Max(1, source.par);
            map = source.map ?? string.Empty;
            links = source.links ?? string.Empty;
            checkpoints = source.checkpoints ?? string.Empty;
            loopSeconds = source.loopSeconds > 0f ? Mathf.Clamp(source.loopSeconds, MinLoopSeconds, MaxLoopSeconds) : 0f;
            rooms = source.rooms ?? string.Empty;
            name = "custom." + displayName;
        }

        /// <summary>The level as a <see cref="CustomLevel"/> record, e.g. to write it as a level code.</summary>
        public CustomLevel ToCustom() => new CustomLevel
        {
            name = displayName, par = par, map = map, links = links, checkpoints = checkpoints,
            loopSeconds = loopSeconds, rooms = rooms,
        };
    }
}
