using TMPro;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>A burst of small squares. The particles are the prefab's own children, so there is nothing to spawn.</summary>
    public sealed class SpriteBurst : MonoBehaviour
    {
        [SerializeField] SpriteRenderer[] particles;
        [SerializeField] Vector2 speedRange = new Vector2(1.2f, 4.8f);
        [SerializeField] Vector2 sizeRange = new Vector2(0.05f, 0.14f);
        [SerializeField] Vector2 lifeRange = new Vector2(0.45f, 0.85f);
        [SerializeField, Tooltip("Velocity kept per second (0 to 1).")] float drag = 0.04f;

        Vector3[] velocity;
        float[] life, maxLife;
        bool playing;

        public bool IsPlaying => playing;

        public void Play(Vector3 localPosition, Color[] colors, int count)
        {
            if (particles == null || particles.Length == 0) return;
            transform.localPosition = localPosition;
            int n = Mathf.Min(count, particles.Length);
            if (velocity == null || velocity.Length != particles.Length)
            {
                velocity = new Vector3[particles.Length];
                life = new float[particles.Length];
                maxLife = new float[particles.Length];
            }
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];
                if (!p) continue;
                bool used = i < n;
                p.gameObject.SetActive(used);
                if (!used) { life[i] = 0f; continue; }
                float a = Random.value * Mathf.PI * 2f, s = Random.Range(speedRange.x, speedRange.y);
                velocity[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * s;
                maxLife[i] = life[i] = Random.Range(lifeRange.x, lifeRange.y);
                p.transform.localPosition = Vector3.zero;
                p.transform.localScale = Vector3.one * Random.Range(sizeRange.x, sizeRange.y);
                p.color = colors != null && colors.Length > 0 ? colors[i % colors.Length] : Color.white;
            }
            gameObject.SetActive(true);
            playing = true;
        }

        void Update()
        {
            if (!playing) return;
            float dt = Time.deltaTime, keep = Mathf.Pow(drag, dt);
            bool any = false;
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i];
                if (!p || life[i] <= 0f) continue;
                life[i] -= dt;
                if (life[i] <= 0f) { p.gameObject.SetActive(false); continue; }
                any = true;
                p.transform.localPosition += velocity[i] * dt;
                velocity[i] *= keep;
                var c = p.color;
                c.a = Mathf.Clamp01(life[i] / maxLife[i]);
                p.color = c;
            }
            if (!any)
            {
                playing = false;
                gameObject.SetActive(false);
            }
        }
    }
}
