using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class ExitView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] rings;
        [SerializeField, Tooltip("Degrees per second for each ring; alternate signs to counter-rotate.")] float[] spinSpeeds = { 80f, -110f, 60f };

        public void Setup(Color color)
        {
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                if (!rings[i]) continue;
                var c = color;
                c.a = Mathf.Clamp01(1f - i * 0.25f);
                rings[i].color = c;
                rings[i].transform.localRotation = Quaternion.Euler(0f, 0f, i * 97f);
            }
        }

        void Update()
        {
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
                if (rings[i] && spinSpeeds != null && i < spinSpeeds.Length)
                    rings[i].transform.Rotate(0f, 0f, spinSpeeds[i] * Time.deltaTime);
        }
    }
}
