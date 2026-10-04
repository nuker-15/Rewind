using Rewind.Data;
using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class CrateView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;

        public void Setup(BoardPalette palette)
        {
            if (sprite) sprite.color = palette.crate;
        }
    }
}
