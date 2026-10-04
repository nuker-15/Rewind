using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>
    /// Fits the board inside the part of the screen the HUD leaves free. Margins are fractions of the screen,
    /// so match them to your HUD layout.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraFramer : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [SerializeField] BoardPalette palette;
        [SerializeField, Range(0f, 0.45f), Tooltip("Screen fraction reserved for the top bar.")] float topMargin = 0.11f;
        [SerializeField, Range(0f, 0.45f), Tooltip("Screen fraction reserved for the timeline.")] float bottomMargin = 0.27f;
        [SerializeField, Range(0f, 0.45f)] float sideMargin = 0.04f;
        [SerializeField, Min(0f), Tooltip("World units of breathing room around the board.")] float padding = 0.5f;

        Camera cam;
        Bounds bounds;
        bool hasBounds;
        int lastWidth, lastHeight;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (palette) cam.backgroundColor = palette.background;
        }

        void OnEnable()
        {
            if (!board) return;
            board.LayoutChanged += OnLayoutChanged;
        }

        void OnDisable()
        {
            if (board) board.LayoutChanged -= OnLayoutChanged;
        }

        void OnLayoutChanged(Bounds b)
        {
            bounds = b;
            hasBounds = true;
            Frame();
        }

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) Frame();
        }

        public void Frame()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (!hasBounds || !cam) return;
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float usableV = Mathf.Max(0.1f, 1f - topMargin - bottomMargin);
            float usableH = Mathf.Max(0.1f, 1f - sideMargin * 2f);
            float w = bounds.size.x + padding * 2f, h = bounds.size.y + padding * 2f;
            float size = Mathf.Max(h / (2f * usableV), w / (2f * aspect * usableH));
            cam.orthographic = true;
            cam.orthographicSize = size;
            // shift the camera so the board sits in the middle of the usable band, not the middle of the screen
            float bandCentre = bottomMargin + usableV * 0.5f;
            float offset = (bandCentre - 0.5f) * 2f * size;
            var p = transform.position;
            transform.position = new Vector3(bounds.center.x, bounds.center.y - offset, p.z);
        }
    }
}
