using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>The prefabs the board is built from. Point this at different prefabs to reskin the board.</summary>
    [CreateAssetMenu(menuName = "Rewind/Board Prefab Set", fileName = "BoardPrefabSet", order = 4)]
    public sealed class BoardPrefabSet : ScriptableObject
    {
        [Header("Tiles")]
        public FloorView floor;
        public WallView wall;
        public PlateView plate;
        public DoorView door;
        public ExitView exit;
        public GroundView ground;
        public GameObject startMarker;

        [Header("Pieces")]
        public BodyView player;
        public BodyView echo;
        public CrateView crate;

        [Header("Markers and effects")]
        public SpriteRenderer trailDot;
        public SpriteRenderer pushChevron;
        public ParadoxMarkerView paradoxMarker;
        public SpriteBurst burst;
        public FloatingLabel floatingLabel;
    }
}
