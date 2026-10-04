using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class FloorView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;

        public void Setup(Vector2Int cell, BoardPalette palette)
        {
            if (sprite) sprite.color = (cell.x + cell.y) % 2 == 1 ? palette.floorA : palette.floorB;
        }
    }
}
