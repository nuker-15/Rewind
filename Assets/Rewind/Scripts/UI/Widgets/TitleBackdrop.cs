using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>Echo squares wandering a grid behind the title, each trailing dots. The pieces are the prefab's children.</summary>
    public sealed class TitleBackdrop : MonoBehaviour
    {
        [SerializeField] RectTransform area;
        [SerializeField] RectTransform[] walkers;
        [SerializeField, Tooltip("trailLength dots per walker, in walker order.")] Image[] trailDots;
        [SerializeField] int trailLength = 8;
        [SerializeField] float cellSize = 80f;
        [SerializeField] float stepSeconds = 0.3f;
        [SerializeField, Range(0f, 1f)] float turnChance = 0.3f;

        static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        Vector2Int[] cells, dirs;
        Vector2Int[][] trails;
        Vector2[] from;
        float timer;

        void OnEnable()
        {
            if (walkers == null) return;
            int n = walkers.Length;
            cells = new Vector2Int[n];
            dirs = new Vector2Int[n];
            from = new Vector2[n];
            trails = new Vector2Int[n][];
            var grid = GridSize();
            for (int i = 0; i < n; i++)
            {
                cells[i] = new Vector2Int(Random.Range(0, grid.x), Random.Range(0, grid.y));
                dirs[i] = Steps[i % Steps.Length];
                from[i] = cells[i];
                trails[i] = new Vector2Int[trailLength];
                for (int k = 0; k < trailLength; k++) trails[i][k] = cells[i];
            }
        }

        Vector2Int GridSize()
        {
            var size = area ? area.rect.size : new Vector2(1920f, 1080f);
            return new Vector2Int(Mathf.Max(2, Mathf.FloorToInt(size.x / cellSize)), Mathf.Max(2, Mathf.FloorToInt(size.y / cellSize)));
        }

        void Update()
        {
            if (cells == null) return;
            timer += Time.unscaledDeltaTime;
            if (timer >= stepSeconds)
            {
                timer -= stepSeconds;
                var grid = GridSize();
                for (int i = 0; i < cells.Length; i++)
                {
                    if (Random.value < turnChance) dirs[i] = Steps[Random.Range(0, Steps.Length)];
                    var next = cells[i] + dirs[i];
                    if (next.x < 0 || next.y < 0 || next.x >= grid.x || next.y >= grid.y)
                    {
                        dirs[i] = -dirs[i];
                        next = cells[i] + dirs[i];
                    }
                    for (int k = trailLength - 1; k > 0; k--) trails[i][k] = trails[i][k - 1];
                    trails[i][0] = cells[i];
                    from[i] = cells[i];
                    cells[i] = next;
                }
            }

            float f = Mathf.Clamp01(timer / Mathf.Min(0.2f, stepSeconds));
            f = 1f - (1f - f) * (1f - f);
            for (int i = 0; i < walkers.Length; i++)
            {
                if (!walkers[i]) continue;
                walkers[i].anchoredPosition = CellToPos(Vector2.Lerp(from[i], cells[i], f));
                for (int k = 0; k < trailLength; k++)
                {
                    int d = i * trailLength + k;
                    if (trailDots == null || d >= trailDots.Length || !trailDots[d]) continue;
                    trailDots[d].rectTransform.anchoredPosition = CellToPos(trails[i][k]);
                }
            }
        }

        Vector2 CellToPos(Vector2 cell) => new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }
}
