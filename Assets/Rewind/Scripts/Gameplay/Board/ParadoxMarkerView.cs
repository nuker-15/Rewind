using Rewind.Data;
using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>Left where an echo vanished: a dashed outline, a cross, and the move it could not make.</summary>
    public sealed class ParadoxMarkerView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer outline;
        [SerializeField] SpriteRenderer cross;
        [SerializeField] SpriteRenderer arrow;
        [SerializeField] float arrowDistance = 0.62f;

        public int Tick { get; private set; }

        public void Setup(Vector2Int gridDir, int tick, Color color)
        {
            Tick = tick;
            if (outline) { var c = color; c.a = 0.75f; outline.color = c; }
            if (cross) cross.color = color;
            if (arrow)
            {
                var d = new Vector3(gridDir.x, -gridDir.y, 0f);
                arrow.transform.localPosition = d * arrowDistance;
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                var c = color; c.a = 0.8f; arrow.color = c;
            }
            SetShown(true);
        }

        public void SetShown(bool shown)
        {
            if (outline) outline.enabled = shown;
            if (cross) cross.enabled = shown;
            if (arrow) arrow.enabled = shown;
        }
    }
}
