using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField, Tooltip("Rotated a quarter turn when the passage runs up-down.")] Transform panelPivot;
        [SerializeField] SpriteRenderer closedPanel;
        [SerializeField, Tooltip("Shown instead of the panel on doors that shut while held.")] SpriteRenderer closedStripes;
        [SerializeField] SpriteRenderer openFrame;
        [SerializeField, Tooltip("One dot per plate when a door listens to several plates.")] SpriteRenderer[] pips;
        [SerializeField] float pipSpacing = 0.22f;
        [SerializeField, Range(0f, 1f)] float openFrameAlpha = 0.8f;

        bool inverse;
        int pipCount;
        bool? open;
        BoardPalette palette;

        public void Setup(Color channelColor, bool inverseDoor, bool passageAcross, int platesOnChannel, BoardPalette boardPalette)
        {
            palette = boardPalette;
            inverse = inverseDoor;
            if (panelPivot) panelPivot.localRotation = passageAcross ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            if (closedPanel) closedPanel.color = channelColor;
            if (closedStripes) closedStripes.color = channelColor;
            if (openFrame) { var c = channelColor; c.a = openFrameAlpha; openFrame.color = c; }

            pipCount = platesOnChannel >= 2 && pips != null ? Mathf.Min(platesOnChannel, pips.Length) : 0;
            if (pips != null)
            {
                for (int i = 0; i < pips.Length; i++)
                {
                    if (!pips[i]) continue;
                    bool used = i < pipCount;
                    pips[i].gameObject.SetActive(used);
                    if (used) pips[i].transform.localPosition = new Vector3((i - (pipCount - 1) * 0.5f) * pipSpacing, 0f, 0f);
                }
            }
            open = null;
        }

        /// <param name="litPips">Which of this door's plates are held, in plate order. May be null.</param>
        public void SetState(bool isOpen, bool[] litPips)
        {
            if (open != isOpen)
            {
                open = isOpen;
                if (closedPanel) closedPanel.enabled = !isOpen && !inverse;
                if (closedStripes) closedStripes.enabled = !isOpen && inverse;
                if (openFrame) openFrame.enabled = isOpen;
            }
            for (int i = 0; i < pipCount; i++)
            {
                bool lit = litPips != null && i < litPips.Length && litPips[i];
                if (pips[i]) pips[i].color = lit ? palette.pipLit : palette.pipDark;
            }
        }
    }
}
