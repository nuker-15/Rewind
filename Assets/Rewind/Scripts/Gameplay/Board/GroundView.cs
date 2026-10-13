using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>The changeable floor of cracked tiles and holes.</summary>
    public sealed class GroundView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer cracks;
        [SerializeField] SpriteRenderer hole;
        [SerializeField] SpriteRenderer holeRim;
        [SerializeField, Tooltip("A sunken crate filling the hole.")] SpriteRenderer filled;

        BoardPalette palette;
        GroundState? state;
        bool tiled;   // drawn with auto-tiles, which include their own rim

        public void Setup(Vector2Int cell, BoardPalette boardPalette)
        {
            palette = boardPalette;
            if (cracks) cracks.flipX = (cell.x + cell.y) % 2 == 1;
            if (hole) hole.color = palette.pit;
            if (holeRim) holeRim.color = palette.wallFace;
            if (filled) filled.color = palette.crate;
            state = null;
        }

        public void SetState(GroundState s)
        {
            if (state == s) return;
            state = s;
            bool isHole = s == GroundState.Hole;
            if (hole) hole.enabled = isHole;
            if (holeRim) holeRim.enabled = isHole && !tiled;
            if (filled) filled.enabled = s == GroundState.Filled;
            if (cracks)
            {
                cracks.enabled = s == GroundState.Cracked || s == GroundState.Stepped;
                cracks.color = s == GroundState.Stepped ? palette.crackStepped : palette.crack;
            }
        }

        /// <summary>Draws the hole with an auto-tile picked from its neighbouring holes, instead of a plain square.</summary>
        public void SetHoleTile(Sprite tile)
        {
            if (!hole || !tile || hole.sprite == tile) return;
            if (!tiled)
            {
                tiled = true;
                hole.color = Color.white;   // the tile carries its own colours
                if (holeRim) holeRim.enabled = false;
            }
            hole.sprite = tile;
            var size = tile.bounds.size;
            if (size.x > 0f && size.y > 0f) hole.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
        }
    }
}
