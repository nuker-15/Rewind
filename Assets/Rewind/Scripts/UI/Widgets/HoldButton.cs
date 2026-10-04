using Rewind.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rewind.UI
{
    public enum PadDirection { Up, Down, Left, Right }

    /// <summary>An on-screen direction button that behaves like a held key.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] UIRouter router;
        [SerializeField] PadDirection direction;
        [SerializeField] Graphic highlight;
        [SerializeField] Color idle = new Color(0.07f, 0.08f, 0.17f, 0.9f);
        [SerializeField] Color held = new Color(0.11f, 0.12f, 0.25f, 1f);

        bool pressed;
        int SourceId => 100 + (int)direction;

        Vector2Int Dir2 => direction switch
        {
            PadDirection.Up => Dir.Up,
            PadDirection.Down => Dir.Down,
            PadDirection.Left => Dir.Left,
            _ => Dir.Right,
        };

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!router || !router.Game) return;
            pressed = true;
            router.Game.PressDirection(Dir2, SourceId);
            if (highlight) highlight.color = held;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();
        void OnDisable() => Release();

        void Release()
        {
            if (!pressed) return;
            pressed = false;
            if (router && router.Game) router.Game.ReleaseDirection(SourceId);
            if (highlight) highlight.color = idle;
        }
    }
}
