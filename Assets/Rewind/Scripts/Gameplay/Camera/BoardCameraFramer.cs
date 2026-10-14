using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>
    /// Frames the room the player is in, inside the part of the screen the HUD leaves free. Rooms are drawn in the level
    /// editor and can be any size, so the zoom follows the room. Walking into another room slides the camera over (the
    /// loop's clock waits for it) or cuts straight there, as the room says. During the rewind it zooms out to the section
    /// being played. A level without rooms is framed whole. Margins are fractions of the screen, so match them to the HUD.
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
        [SerializeField, Min(0f), Tooltip("Seconds the camera takes to slide to another room or zoom for the rewind. The loop's clock waits meanwhile.")]
        float moveSeconds = 0.35f;

        Camera cam;
        bool hasLevel, wasManual;
        List<Room> rooms = new List<Room>();
        int room;
        bool wide;                       // zoomed out for the rewind
        Vector3 fromPos, toPos;
        float fromSize, toSize, moveT = 1f;
        int lastWidth, lastHeight;

        /// <summary>Leaves the camera to someone else, e.g. the level editor's pan and zoom. The clock never waits meanwhile.</summary>
        public bool ManualControl { get; set; }

        RewindGame Game => board ? board.Game : null;

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
            var game = Game;
            if (game) game.ClockHeld = false;
        }

        // A level was loaded (or the editor's preview of one rebuilt): jump straight to the player's room.
        void OnLayoutChanged(Bounds bounds)
        {
            var game = Game;
            hasLevel = game && game.HasLevel;
            if (!hasLevel) return;
            rooms = game.CurrentLevel.RoomsFor(game.Session.Layout);
            room = Mathf.Max(0, LevelRooms.IndexOf(rooms, game.Session.World.Player.Cell));
            wide = WantsWide(game);
            if (!ManualControl) Snap();
        }

        bool WantsWide(RewindGame game) => game.Phase == LoopPhase.Rewinding && rooms.Count > 1;

        /// <summary>Frames the whole level at once, whatever the rooms.</summary>
        public void FitWholeLevel()
        {
            if (!cam || !board) return;
            cam.orthographic = true;
            Fit(board.WorldBounds, out var pos, out float size);
            transform.position = pos;
            cam.orthographicSize = size;
        }

        void LateUpdate()
        {
            var game = Game;
            if (!hasLevel || !game || !game.HasLevel) return;
            if (ManualControl)
            {
                wasManual = true;
                game.ClockHeld = false;
                return;
            }
            if (wasManual)
            {
                wasManual = false;
                Snap();
            }

            bool move = false;
            bool wantWide = WantsWide(game);
            if (wantWide != wide)
            {
                wide = wantWide;
                move = true;
            }
            // a doorway in a shared wall belongs to both rooms, so standing in one never flips the camera back and forth
            int now = LevelRooms.IndexOf(rooms, game.Session.World.Player.Cell, room);
            if (now >= 0 && now != room)
            {
                room = now;
                move |= !wide;
            }
            if (move)
            {
                if (!wide && rooms[room].Transition == RoomTransition.Cut) Snap();
                else StartMove();
            }
            else if (Screen.width != lastWidth || Screen.height != lastHeight) Snap();

            if (moveT < 1f)
            {
                moveT = moveSeconds > 0f ? Mathf.Min(1f, moveT + Time.unscaledDeltaTime / moveSeconds) : 1f;
                float k = Mathf.SmoothStep(0f, 1f, moveT);
                transform.position = Vector3.Lerp(fromPos, toPos, k);
                cam.orthographicSize = Mathf.Lerp(fromSize, toSize, k);
            }
            game.ClockHeld = moveT < 1f;
        }

        void Snap()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (!hasLevel || !cam) return;
            cam.orthographic = true;
            Fit(Target(), out var pos, out float size);
            transform.position = pos;
            cam.orthographicSize = size;
            moveT = 1f;
        }

        void StartMove()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            fromPos = transform.position;
            fromSize = cam.orthographicSize;
            Fit(Target(), out toPos, out toSize);
            bool already = (toPos - fromPos).sqrMagnitude < 1e-6f && Mathf.Abs(toSize - fromSize) < 1e-4f;
            moveT = already ? 1f : 0f;   // nothing to move, so the clock need not wait
        }

        Bounds Target()
        {
            var b = board.CellBounds(rooms[Mathf.Clamp(room, 0, rooms.Count - 1)].Cells);
            if (!wide) return b;
            // the rewind shows the section being played (the whole level without checkpoints), and never less than the room
            var layout = Game.Session.Layout;
            b.Encapsulate(layout.SectionCount == 1 ? board.WorldBounds : board.CellBounds(layout.SectionBounds(Game.Session.CurrentSection)));
            return b;
        }

        void Fit(Bounds b, out Vector3 pos, out float size)
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float usableV = Mathf.Max(0.1f, 1f - topMargin - bottomMargin);
            float usableH = Mathf.Max(0.1f, 1f - sideMargin * 2f);
            float w = b.size.x + padding * 2f, h = b.size.y + padding * 2f;
            size = Mathf.Max(h / (2f * usableV), w / (2f * aspect * usableH));
            // shift the camera so the board sits in the middle of the usable band, not the middle of the screen
            float bandCentre = bottomMargin + usableV * 0.5f;
            float offset = (bandCentre - 0.5f) * 2f * size;
            pos = new Vector3(b.center.x, b.center.y - offset, transform.position.z);
        }
    }
}
