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

        /// <summary>Swaps in a tileset's floor sprite, scaled to fill exactly one cell whatever its resolution.</summary>
        public void SetTile(Sprite tile)
        {
            if (!sprite || !tile) return;
            sprite.sprite = tile;
            var size = tile.bounds.size;
            if (size.x > 0f && size.y > 0f) sprite.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
        }
    }
}
