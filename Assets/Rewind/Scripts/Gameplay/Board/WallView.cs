using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class WallView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField, Tooltip("Highlight shown when the cell above is open.")] SpriteRenderer topEdge;
        [SerializeField, Tooltip("Front face shown when the cell below is open.")] SpriteRenderer face;

        public void Setup(bool openAbove, bool openBelow, BoardPalette palette)
        {
            if (body) body.color = palette.wall;
            if (topEdge) { topEdge.color = palette.wallTop; topEdge.enabled = openAbove; }
            if (face) { face.color = palette.wallFace; face.enabled = openBelow; }
        }
    }
}
