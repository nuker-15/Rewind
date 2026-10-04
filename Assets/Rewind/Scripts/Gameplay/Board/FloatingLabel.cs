using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>Text that drifts up and fades, such as PARADOX.</summary>
    public sealed class FloatingLabel : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float riseSpeed = 0.7f;
        [SerializeField] float lifetime = 1.8f;

        float age;

        public bool IsPlaying => gameObject.activeSelf;

        public void Play(Vector3 localPosition, string text, Color color)
        {
            transform.localPosition = localPosition;
            if (label)
            {
                label.text = text;
                label.color = color;
            }
            age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.localPosition += Vector3.up * (riseSpeed * Time.deltaTime);
            if (label)
            {
                var c = label.color;
                c.a = Mathf.Clamp01(1f - age / lifetime);
                label.color = c;
            }
            if (age >= lifetime) gameObject.SetActive(false);
        }
    }
}
