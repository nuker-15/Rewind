using System.Collections.Generic;
using Rewind.Core;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>The tape strip under the board: one row per echo plus the live recording, and a playhead.</summary>
    public sealed class TimelineView : MonoBehaviour
    {
        [SerializeField] UIRouter router;
        [SerializeField] BoardPalette palette;
        [SerializeField] TimelineTrack trackPrefab;
        [SerializeField, Tooltip("Parent of the rows; should have a VerticalLayoutGroup.")] RectTransform rows;
        [SerializeField, Tooltip("Spans the track area horizontally; the playhead is positioned inside it.")] RectTransform scale;
        [SerializeField] RectTransform playhead;
        [SerializeField] Graphic playheadGraphic;
        [SerializeField] float rowHeight = 22f;
        [SerializeField] float minRowHeight = 12f;
        [SerializeField, Tooltip("Rows shrink to stay within this height.")] float maxRowsHeight = 170f;

        readonly List<TimelineTrack> tracks = new List<TimelineTrack>();
        RewindGame game;

        void OnEnable()
        {
            game = router ? router.Game : null;
            if (!game) return;
            game.LoopReady += OnLoopReady;
            if (game.HasLevel) Rebuild();
        }

        void OnDisable()
        {
            if (game) game.LoopReady -= OnLoopReady;
        }

        void OnLoopReady(int loopNumber) => Rebuild();

        void Rebuild()
        {
            if (!game || !game.HasLevel || !trackPrefab || !rows) return;
            int count = game.Session.World.Bodies.Count;
            float height = Mathf.Clamp(maxRowsHeight / Mathf.Max(1, count), minRowHeight, rowHeight);
            while (tracks.Count < count) tracks.Add(Instantiate(trackPrefab, rows));
            for (int i = 0; i < tracks.Count; i++)
            {
                bool used = i < count;
                tracks[i].gameObject.SetActive(used);
                if (!used) continue;
                bool player = i == count - 1;
                tracks[i].Setup(player ? "YOU" : $"ECHO {i + 1}", player ? palette.record : palette.Echo(i), height);
            }
        }

        void LateUpdate()
        {
            if (!game || !game.HasLevel) return;
            var session = game.Session;
            var bodies = session.World.Bodies;
            float viewTick = game.ViewTick;
            int loopTicks = session.LoopTicks;
            bool rewinding = game.Phase == LoopPhase.Rewinding;

            for (int i = 0; i < bodies.Count && i < tracks.Count; i++)
            {
                var b = bodies[i];
                if (b.Kind == BodyKind.Player)
                {
                    var rec = session.Recording;
                    tracks[i].Show(rec, Mathf.Min(rec.Length, Mathf.CeilToInt(viewTick)), loopTicks, -1, null, palette.record);
                }
                else if (b.EchoIndex < session.Echoes.Count)
                {
                    var rec = session.Echoes[b.EchoIndex];
                    bool cut = !b.Alive && viewTick >= b.DeadAtTick + 0.5f;
                    tracks[i].Show(rec, rec.Length, loopTicks, cut ? b.DeadAtTick : -1, cut ? ParadoxText.Describe(b.Reason) : null, palette.Echo(b.EchoIndex));
                }
            }

            if (playhead && scale)
            {
                float f = Mathf.Clamp01(viewTick / loopTicks);
                playhead.anchorMin = new Vector2(f, playhead.anchorMin.y);
                playhead.anchorMax = new Vector2(f, playhead.anchorMax.y);
                playhead.anchoredPosition = new Vector2(0f, playhead.anchoredPosition.y);
            }
            if (playheadGraphic) playheadGraphic.color = rewinding ? palette.rewind : palette.pipLit;
        }
    }
}
