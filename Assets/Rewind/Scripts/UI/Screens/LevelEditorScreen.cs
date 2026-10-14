using System;
using System.Collections.Generic;
using System.Linq;
using Rewind.Core;
using Rewind.Data;
using Rewind.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>
    /// The in-game level editor. You paint straight onto the real board (it is loaded as a custom level, so what you
    /// see is what you play), test-play, keep levels on this device and share them as one-line codes, which the Unity
    /// Level Editor window can import into the project. The UI is built in code, so it needs no prefab: the router
    /// creates it at start-up.
    /// </summary>
    public sealed class LevelEditorScreen : UIScreen
    {
        // shortcut: 200 x 200 keeps a loop's rewind snapshots (a copy of the ground per tick) to tens of MB; snapshot only
        // the changing cells before going bigger
        const int MinWidth = 5, MinHeight = 4, MaxWidth = 200, MaxHeight = 200, GrowStep = 10;
        const int NewWidth = 20, NewHeight = 11, UndoLimit = 80, RowsPerPage = 6;
        const float StatusSeconds = 4f, SaveDelay = 1f, MinViewSize = 3f;

        enum Tool { Brush, Rectangle, Fill, Link, Room }

        readonly struct Swatch
        {
            public readonly char Glyph;
            public readonly string Name;
            public Swatch(char glyph, string name) { Glyph = glyph; Name = name; }
        }

        static readonly Swatch[] Swatches =
        {
            new Swatch('#', "Wall"), new Swatch('.', "Floor (eraser)"), new Swatch('S', "Start"), new Swatch('X', "Exit"),
            new Swatch('o', "Crate"), new Swatch('~', "Cracked floor"), new Swatch('_', "Hole"),
            new Swatch('p', "Plate"), new Swatch('d', "Door, open while its plates are held"), new Swatch('D', "Door, shut while its plates are held"),
            new Swatch(LevelCheckpoints.Glyph, "Checkpoint, a new spawn and timer"),
        };

        static readonly string[] ToolNames = { "BRUSH", "RECT", "FILL", "LINK", "ROOM" };
        static readonly Color RoomTint = new Color(0.28f, 0.85f, 1f, 0.35f);
        static readonly Color ActiveTool = new Color(0.5f, 0.15f, 0.2f);
        static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
        static Sprite whiteSprite;

        // what is being edited
        CustomLevel level;
        char[][] grid;
        readonly List<(string map, string links, string checkpoints, string rooms)> undo = new List<(string, string, string, string)>();
        char glyph = '#';
        Tool tool = Tool.Brush;
        Vector2Int? linkPlate;   // the plate the LINK tool is connecting doors to
        int editing = -1;        // which section PAR and TIME change: -1 the start, else a checkpoint in reading order
        int pickedRoom = -1;     // the room whose camera setting the panel shows
        float saveAt = -1f;      // edits are written to disk a moment after the last one, not on every stroke

        // painting
        bool stroking, erasing, strokeChanged, previewDirty, fitPending;
        Vector2Int strokeStart, strokeLast;
        Vector2Int? roomStart;   // where a ROOM drag began
        char[][] strokeBase;
        LevelData preview, testLevel;
        BoardView board;
        SpriteRenderer cursor, linkMark, checkpointMark;
        // While a stroke is under way the board is not rebuilt: painted cells are shown as plain squares until release.
        readonly List<SpriteRenderer> strokeMarks = new List<SpriteRenderer>();
        int strokeMarksUsed;

        // the view: pan and zoom
        bool panning, pinching;
        Vector2 panLast, pinchMid;
        float pinchSpread;

        // ui
        TMP_InputField nameField;
        TextMeshProUGUI parLabel, roomLabel, transitionLabel, editingLabel, timeLabel, statusLabel, hintLabel, pageLabel;
        TMP_InputField mapWidthField, mapHeightField;
        readonly List<SpriteRenderer> roomLines = new List<SpriteRenderer>();
        BoardCameraFramer framer;
        readonly List<(Image fill, Image frame, TextMeshProUGUI text, char glyph)> swatchViews = new List<(Image, Image, TextMeshProUGUI, char)>();
        readonly Image[] toolImages = new Image[ToolNames.Length];
        RectTransform levelsPanel, levelsList;
        int page;
        string pendingDelete, solveNote;
        float statusUntil = -1f;

        int W => grid[0].Length;
        int H => grid.Length;
        char Active => erasing ? '.' : glyph;
        bool LevelsOpen => levelsPanel && levelsPanel.gameObject.activeSelf;

        // ---- Construction ------------------------------------------------------------------------------

        public static LevelEditorScreen Build(UIRouter router)
        {
            var root = RuntimeUi.Rect("LevelEditorScreen", router.transform);
            root.gameObject.SetActive(false);   // build it all inactive so nothing wakes up half-made
            RuntimeUi.Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var screen = root.gameObject.AddComponent<LevelEditorScreen>();
            screen.Bind(router);
            var crt = router.transform.Find("CRT");   // stay under the scanline overlay
            if (crt) root.SetSiblingIndex(crt.GetSiblingIndex());
            screen.BuildTopBar(root);
            screen.BuildBottomPanel(root);
            screen.BuildLevelsPanel(root);
            screen.CloseImmediate();
            return screen;
        }

        void BuildTopBar(RectTransform root)
        {
            var bar = RuntimeUi.Rect("TopBar", root);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(24f, -104f);
            bar.offsetMax = new Vector2(-24f, -12f);
            RuntimeUi.Fill(bar, RuntimeUi.WithAlpha(RuntimeUi.Bezel, 0.94f), true);
            RuntimeUi.Row(bar, 12f).padding = new RectOffset(20, 20, 14, 14);

            RuntimeUi.Size(RuntimeUi.Label(bar, "Title", "LEVEL EDITOR", 30f, RuntimeUi.Cyan, TextAlignmentOptions.MidlineLeft), 240f, 64f);
            nameField = RuntimeUi.InputField(bar, "Level name", 380f, 60f, 28);
            nameField.onEndEdit.AddListener(OnNameEdited);
            RuntimeUi.Size(RuntimeUi.Label(bar, "ParTitle", "PAR", 24f, RuntimeUi.Muted, TextAlignmentOptions.MidlineRight), 60f, 60f);
            RuntimeUi.Button(bar, "-", 52f, 60f, () => ChangePar(-1), 30f);
            parLabel = RuntimeUi.Label(bar, "Par", "2", 30f, RuntimeUi.Fg, TextAlignmentOptions.Center);
            RuntimeUi.Size(parLabel, 44f, 60f);
            RuntimeUi.Button(bar, "+", 52f, 60f, () => ChangePar(1), 30f);
            RuntimeUi.Flexible(bar);
            RuntimeUi.Button(bar, "TEST PLAY", 170f, 60f, TestPlay);
            RuntimeUi.Button(bar, "MY LEVELS", 170f, 60f, () => ShowLevels(true));
            RuntimeUi.Button(bar, "COPY CODE", 170f, 60f, CopyCode);
            RuntimeUi.Button(bar, "PASTE CODE", 170f, 60f, PasteCode);
            RuntimeUi.Button(bar, "EXIT", 110f, 60f, () => router.ShowTitle());
        }

        void BuildBottomPanel(RectTransform root)
        {
            var panel = RuntimeUi.Rect("BottomPanel", root);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.offsetMin = new Vector2(24f, 12f);
            panel.offsetMax = new Vector2(-24f, 266f);
            RuntimeUi.Fill(panel, RuntimeUi.WithAlpha(RuntimeUi.Bezel, 0.94f), true);
            RuntimeUi.Column(panel, 8f, new RectOffset(20, 20, 12, 10));

            // tiles and tools
            var tiles = RuntimeUi.Rect("Tiles", panel);
            RuntimeUi.Row(tiles, 8f);
            RuntimeUi.Size(tiles, -1f, 76f);
            foreach (var s in Swatches) BuildSwatch(tiles, s);
            RuntimeUi.Flexible(tiles);
            for (int i = 0; i < ToolNames.Length; i++)
            {
                var t = (Tool)i;
                toolImages[i] = RuntimeUi.Button(tiles, ToolNames[i], 116f, 64f, () => SetTool(t)).image;
            }
            RuntimeUi.Button(tiles, "UNDO", 116f, 64f, Undo);

            // the canvas: its size (typed), growing it by solid wall on any side, and trimming the wall around the level
            var canvas = RuntimeUi.Rect("Canvas", panel);
            RuntimeUi.Row(canvas, 8f);
            RuntimeUi.Size(canvas, -1f, 48f);
            (mapWidthField, mapHeightField) = SizeFields(canvas, "MAP", ResizeMap);
            SideButtons(canvas, "GROW", GrowMap);
            RuntimeUi.Button(canvas, "TRIM", 100f, 46f, TrimMap);
            RuntimeUi.Button(canvas, "WALL BORDER", 190f, 46f, WallBorder);
            RuntimeUi.Button(canvas, "CLEAR", 120f, 46f, ClearLevel);
            RuntimeUi.Flexible(canvas);
            statusLabel = RuntimeUi.Label(canvas, "Status", "", 24f, RuntimeUi.Amber, TextAlignmentOptions.MidlineRight);
            RuntimeUi.Size(statusLabel, 360f, 48f);

            // which section PAR (top bar) and TIME belong to, the picked room's camera, and the view
            var rules = RuntimeUi.Rect("Rules", panel);
            RuntimeUi.Row(rules, 8f);
            RuntimeUi.Size(rules, -1f, 48f);
            RuntimeUi.Button(rules, "<", 60f, 46f, () => ChangeEditing(-1));
            editingLabel = RuntimeUi.Label(rules, "Editing", "", 24f, RuntimeUi.Cyan, TextAlignmentOptions.Center);
            RuntimeUi.Size(editingLabel, 240f, 48f);
            RuntimeUi.Button(rules, ">", 60f, 46f, () => ChangeEditing(1));
            RuntimeUi.Button(rules, "- TIME", 110f, 46f, () => ChangeTime(-1));
            timeLabel = RuntimeUi.Label(rules, "Time", "", 26f, RuntimeUi.Fg, TextAlignmentOptions.Center);
            RuntimeUi.Size(timeLabel, 120f, 48f);
            RuntimeUi.Button(rules, "+ TIME", 110f, 46f, () => ChangeTime(1));
            RuntimeUi.Flexible(rules);
            roomLabel = RuntimeUi.Label(rules, "RoomLabel", "", 24f, RuntimeUi.Muted, TextAlignmentOptions.MidlineRight);
            RuntimeUi.Size(roomLabel, 300f, 48f);
            RuntimeUi.Button(rules, "", 230f, 46f, ToggleTransition, out transitionLabel);
            RuntimeUi.Button(rules, "-", 60f, 46f, () => ZoomView(1.25f));
            RuntimeUi.Button(rules, "FIT", 90f, 46f, FitView);
            RuntimeUi.Button(rules, "+", 60f, 46f, () => ZoomView(0.8f));

            hintLabel = RuntimeUi.Label(panel, "Hint", "", 21f, RuntimeUi.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            RuntimeUi.Size(hintLabel, -1f, 30f);
        }

        /// <summary>A title and two number fields, W x H. Pressing enter in either (or leaving it) applies both.</summary>
        (TMP_InputField width, TMP_InputField height) SizeFields(RectTransform parent, string title, Action<int, int> apply)
        {
            RuntimeUi.Size(RuntimeUi.Label(parent, title, title, 24f, RuntimeUi.Muted, TextAlignmentOptions.MidlineRight), 80f, 48f);
            var w = NumberField(parent);
            RuntimeUi.Size(RuntimeUi.Label(parent, "By", "x", 24f, RuntimeUi.Muted, TextAlignmentOptions.Center), 20f, 48f);
            var h = NumberField(parent);
            void OnEdited(string _)
            {
                if (int.TryParse(w.text, out int wv) && int.TryParse(h.text, out int hv)) apply(wv, hv);
                else RefreshInfo();   // not a number: show the current size again
            }
            w.onEndEdit.AddListener(OnEdited);
            h.onEndEdit.AddListener(OnEdited);
            return (w, h);
        }

        static TMP_InputField NumberField(RectTransform parent)
        {
            var field = RuntimeUi.InputField(parent, "", 84f, 46f, 3);
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.textComponent.alignment = TextAlignmentOptions.Center;
            return field;
        }

        /// <summary>A title and one button per side.</summary>
        static void SideButtons(RectTransform parent, string title, Action<Vector2Int> act)
        {
            RuntimeUi.Size(RuntimeUi.Label(parent, title, title, 24f, RuntimeUi.Muted, TextAlignmentOptions.MidlineRight), 60f, 48f);
            RuntimeUi.Button(parent, "LEFT", 100f, 46f, () => act(Dir.Left));
            RuntimeUi.Button(parent, "RIGHT", 110f, 46f, () => act(Dir.Right));
            RuntimeUi.Button(parent, "UP", 80f, 46f, () => act(Dir.Up));
            RuntimeUi.Button(parent, "DOWN", 100f, 46f, () => act(Dir.Down));
        }

        void BuildSwatch(RectTransform parent, Swatch s)
        {
            var rt = RuntimeUi.Rect("Tile " + s.Glyph, parent);
            RuntimeUi.Size(rt, 70f, 70f);
            var frame = RuntimeUi.Fill(rt, Color.clear, true);   // the selection ring, and what catches the click
            var inner = RuntimeUi.Rect("Fill", rt);
            RuntimeUi.Stretch(inner, 5f, 5f, 5f, 5f);
            var fill = RuntimeUi.Fill(inner, Color.gray, false);
            var text = RuntimeUi.Label(inner, "Glyph", s.Glyph.ToString(), 30f, RuntimeUi.Fg, TextAlignmentOptions.Center);
            RuntimeUi.Stretch(text.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            char g = s.Glyph;
            button.onClick.AddListener(() => SelectGlyph(g));
            button.onClick.AddListener(UIEvents.RaiseActivated);
            swatchViews.Add((fill, frame, text, g));
        }

        void BuildLevelsPanel(RectTransform root)
        {
            levelsPanel = RuntimeUi.Rect("MyLevels", root);
            RuntimeUi.Stretch(levelsPanel);
            RuntimeUi.Fill(levelsPanel, RuntimeUi.WithAlpha(RuntimeUi.Ink, 0.82f), true);
            var card = RuntimeUi.Place(RuntimeUi.Rect("Card", levelsPanel), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 720f));
            RuntimeUi.Fill(card, RuntimeUi.Bezel, true);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = RuntimeUi.Line;
            outline.effectDistance = new Vector2(3f, -3f);
            RuntimeUi.Column(card, 12f, new RectOffset(36, 36, 28, 28));

            RuntimeUi.Size(RuntimeUi.Label(card, "Title", "MY LEVELS", 44f, RuntimeUi.Fg, TextAlignmentOptions.MidlineLeft), -1f, 60f);
            RuntimeUi.Size(RuntimeUi.Label(card, "Sub", "Saved on this device. COPY CODE shares a level; the Unity Level Editor window can import codes into the project.",
                22f, RuntimeUi.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal), -1f, 30f);
            levelsList = RuntimeUi.Rect("List", card);
            RuntimeUi.Column(levelsList, 8f);
            RuntimeUi.Size(levelsList, -1f, RowsPerPage * 64f + (RowsPerPage - 1) * 8f);

            var footer = RuntimeUi.Rect("Footer", card);
            RuntimeUi.Row(footer, 10f);
            RuntimeUi.Size(footer, -1f, 64f);
            RuntimeUi.Button(footer, "< PREV", 130f, 60f, () => TurnPage(-1));
            pageLabel = RuntimeUi.Label(footer, "Page", "1 / 1", 24f, RuntimeUi.Muted, TextAlignmentOptions.Center);
            RuntimeUi.Size(pageLabel, 110f, 60f);
            RuntimeUi.Button(footer, "NEXT >", 130f, 60f, () => TurnPage(1));
            RuntimeUi.Flexible(footer);
            RuntimeUi.Button(footer, "NEW LEVEL", 190f, 60f, NewLevel);
            RuntimeUi.Button(footer, "CLOSE", 140f, 60f, () => ShowLevels(false));
            levelsPanel.gameObject.SetActive(false);
        }

        // ---- Screen lifecycle ----------------------------------------------------------------------------

        protected override void OnOpened()
        {
            if (!board) board = FindFirstObjectByType<BoardView>();
            if (!framer) framer = FindFirstObjectByType<BoardCameraFramer>();
            if (framer) framer.ManualControl = true;   // the editor pans and zooms the camera itself
            ShowLevels(false);
            if (level == null) OpenLevel(CustomLevelStore.Get(CustomLevelStore.LastEditedId) ?? CreateLevel(NewWidth, NewHeight));
            else
            {
                previewDirty = fitPending = true;
                RefreshAll();
            }
            RecolorSwatches();
            if (!string.IsNullOrEmpty(solveNote))
            {
                SetStatus(solveNote, 8f);
                solveNote = null;
            }
        }

        protected override void OnClosed()
        {
            if (stroking) Restore(strokeBase);   // a stroke cut short by closing is dropped
            stroking = false;
            strokeBase = null;
            roomStart = null;
            ClearStrokeMarks();
            HideCursor();
            Hide(linkMark);
            Hide(checkpointMark);
            foreach (var line in roomLines) Hide(line);
            if (framer) framer.ManualControl = false;
            FlushSave();
        }

        void OnDestroy()
        {
            FlushSave();
            if (preview) Destroy(preview);
            if (testLevel) Destroy(testLevel);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) FlushSave();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) FlushSave();
        }

        /// <summary>Notes that the level changed; it is written to disk a moment after the last change.</summary>
        void MarkUnsaved() => saveAt = Time.unscaledTime + SaveDelay;

        void FlushSave()
        {
            if (saveAt < 0f || level == null) return;
            saveAt = -1f;
            CustomLevelStore.Save(level);
        }

        /// <summary>Escape: close the level list, or leave the editor.</summary>
        public void HandleBack()
        {
            if (LevelsOpen) ShowLevels(false);
            else router.ShowTitle();
        }

        /// <summary>Called by the router when a test run of the level being edited is solved.</summary>
        public void RecordSolve(int loops)
        {
            if (level == null) return;
            if (level.bestLoops == 0 || loops < level.bestLoops)
            {
                level.bestLoops = loops;
                CustomLevelStore.Save(level);
            }
            int par = TotalPar;
            solveNote = loops == par ? $"SOLVED AT PAR: {loops} LOOPS."
                : loops < par ? $"SOLVED IN {loops} LOOPS, UNDER PAR {par}. LOWER THE PAR?"
                : $"SOLVED IN {loops} LOOPS, OVER PAR {par}.";
        }

        protected override void OnTick()
        {
            if (grid == null) return;
            if (LevelsOpen) HideCursor();
            else
            {
                HandleKeys();
                if (!HandleView()) HandlePointer();
            }
            ShowLinkMark();
            // the board is rebuilt once a stroke ends, never during one
            if (previewDirty && !stroking) Preview();
            if (saveAt >= 0f && Time.unscaledTime >= saveAt) FlushSave();
            if (statusUntil >= 0f && Time.unscaledTime > statusUntil)
            {
                statusUntil = -1f;
                statusLabel.text = string.Empty;
            }
        }

        // ---- Input ---------------------------------------------------------------------------------------------

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null || UIFocus.IsTyping) return;
            bool ctrl = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (ctrl && kb.zKey.wasPressedThisFrame) Undo();
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            var cam = Camera.main;
            if (pointer == null || !cam || !board)
            {
                HideCursor();
                return;
            }
            var mouse = Mouse.current;
            Vector2 screen = pointer.position.ReadValue();
            bool leftHeld = pointer.press.isPressed;
            bool rightHeld = mouse != null && mouse.rightButton.isPressed;
            var cell = ScreenToCell(cam, screen);

            var clamped = new Vector2Int(Mathf.Clamp(cell.x, 0, W - 1), Mathf.Clamp(cell.y, 0, H - 1));
            if (stroking)
            {
                // only the rectangle snaps to the board's edge; a brush dragged off the board paints nothing out there
                if (leftHeld || rightHeld) ContinueStroke(tool == Tool.Rectangle ? clamped : cell);
                else EndStroke();
                if (!stroking) HideCursor();
                else if (tool == Tool.Rectangle) ShowMarker(ref cursor, "EditorCursor", 30, strokeStart, clamped, PaintTint());   // the rectangle as it will be painted
                else ShowCursor(clamped, clamped, erasing);
                return;
            }
            if (roomStart is Vector2Int start)
            {
                if (leftHeld) ShowMarker(ref cursor, "EditorCursor", 30, start, clamped, RoomTint);
                else
                {
                    roomStart = null;
                    HideCursor();
                    FinishRoom(start, clamped);
                }
                return;
            }

            bool inside = cell.x >= 0 && cell.y >= 0 && cell.x < W && cell.y < H;
            if (!inside || OverUi(screen))
            {
                HideCursor();
                return;
            }
            ShowCursor(cell, cell, false);
            bool leftDown = pointer.press.wasPressedThisFrame;
            bool rightDown = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if (tool == Tool.Link)
            {
                if (leftDown) LinkClick(cell);
                else if (rightDown) linkPlate = null;
                return;
            }
            if (tool == Tool.Room)
            {
                if (leftDown) roomStart = cell;
                else if (rightDown) RemoveRoom(cell);
                return;
            }
            if (leftDown || rightDown) BeginStroke(cell, rightDown && !leftDown);
        }

        Color PaintTint()
        {
            var c = SwatchColor(Active, board ? board.Palette : null);
            c.a = 0.6f;
            return c;
        }

        // ---- View: pan and zoom --------------------------------------------------------------------------------

        /// <summary>
        /// Wheel or pinch zooms, middle drag, space + drag or a two-finger drag pans. Returns true while the pointer is
        /// busy moving the view, so it paints nothing.
        /// </summary>
        bool HandleView()
        {
            var cam = Camera.main;
            var pointer = Pointer.current;
            if (!cam || !board || pointer == null) return false;
            if (Pinch(cam)) return true;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            Vector2 screen = pointer.position.ReadValue();
            if (mouse != null && !stroking && !OverUi(screen))
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) ZoomAt(cam, screen, scroll > 0f ? 1f / 1.15f : 1.15f);
            }
            bool spaceDrag = kb != null && kb.spaceKey.isPressed && !UIFocus.IsTyping && pointer.press.isPressed;
            bool drag = !stroking && roomStart == null && (spaceDrag || mouse != null && mouse.middleButton.isPressed);
            if (drag && (panning || !OverUi(screen)))
            {
                if (panning) Pan(cam, screen - panLast);
                panning = true;
                panLast = screen;
                HideCursor();
                return true;
            }
            panning = false;
            return kb != null && kb.spaceKey.isPressed && !UIFocus.IsTyping;   // holding space means panning, not painting
        }

        // Two fingers: pinch to zoom, drag to pan. A stroke the first finger started is dropped.
        bool Pinch(Camera cam)
        {
            var touch = Touchscreen.current;
            int count = 0;
            Vector2 a = default, b = default;
            if (touch != null)
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (count == 0) a = t.position.ReadValue();
                    else if (count == 1) b = t.position.ReadValue();
                    count++;
                }
            if (count < 2)
            {
                pinching = false;
                return false;
            }
            if (stroking)
            {
                Restore(strokeBase);
                stroking = false;
                strokeBase = null;
                ClearStrokeMarks();
            }
            roomStart = null;
            HideCursor();
            var mid = (a + b) * 0.5f;
            float spread = Mathf.Max(1f, Vector2.Distance(a, b));
            if (pinching)
            {
                ZoomAt(cam, mid, pinchSpread / spread);
                Pan(cam, mid - pinchMid);
            }
            pinching = true;
            pinchMid = mid;
            pinchSpread = spread;
            return true;
        }

        /// <summary>Zooms by a factor (below 1 zooms in) keeping the point under the screen position still.</summary>
        void ZoomAt(Camera cam, Vector2 screen, float factor)
        {
            var before = cam.ScreenToWorldPoint(screen);
            float most = Mathf.Max(MinViewSize * 2f, Mathf.Max(W, H));
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, MinViewSize, most);
            cam.transform.position += before - cam.ScreenToWorldPoint(screen);
            KeepBoardInView(cam);
        }

        void Pan(Camera cam, Vector2 screenDelta)
        {
            float unitsPerPixel = cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);
            cam.transform.position -= (Vector3)(screenDelta * unitsPerPixel);
            KeepBoardInView(cam);
        }

        // the middle of the screen never leaves the canvas, so it can always be found again
        void KeepBoardInView(Camera cam)
        {
            var b = board.WorldBounds;
            var p = cam.transform.position;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.y = Mathf.Clamp(p.y, b.min.y, b.max.y);
            cam.transform.position = p;
        }

        void ZoomView(float factor)
        {
            var cam = Camera.main;
            if (cam && board) ZoomAt(cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), factor);
        }

        void FitView()
        {
            if (framer) framer.FitWholeLevel();
        }

        // ---- Rooms -----------------------------------------------------------------------------------------------

        /// <summary>
        /// ROOM tool: a click picks the room under it; a drag draws a new room. Drawn into solid wall, the room is carved
        /// out (walls around, floor inside); drawn over a built area, it only marks what the camera frames.
        /// </summary>
        void FinishRoom(Vector2Int a, Vector2Int b)
        {
            var cells = new RectInt(Vector2Int.Min(a, b), Vector2Int.Max(a, b) - Vector2Int.Min(a, b) + Vector2Int.one);
            if (cells.width == 1 && cells.height == 1)
            {
                pickedRoom = LevelRooms.IndexOf(LevelRooms.Parse(level.rooms), a);
                RefreshInfo();
                return;
            }
            string before = MapText(grid), rooms = level.rooms;
            string error = LevelRooms.TryAdd(grid, ref rooms, cells);
            if (error != null)
            {
                SetStatus(error);
                return;
            }
            Remember(before);
            level.rooms = rooms;
            pickedRoom = LevelRooms.Parse(rooms).Count - 1;
            Commit();
            SetStatus($"ROOM {pickedRoom + 1} ADDED, {cells.width} x {cells.height}. OPEN DOORWAYS IN ITS WALLS.");
        }

        void RemoveRoom(Vector2Int cell)
        {
            string rooms = level.rooms;
            if (!LevelRooms.TryRemove(ref rooms, cell))
            {
                SetStatus("THERE IS NO ROOM HERE.");
                return;
            }
            Remember(MapText(grid));
            level.rooms = rooms;
            pickedRoom = -1;
            Commit();
            SetStatus("ROOM REMOVED. ITS TILES STAY.");
        }

        void ToggleTransition()
        {
            var rooms = LevelRooms.Parse(level?.rooms);
            if (pickedRoom < 0 || pickedRoom >= rooms.Count)
            {
                SetStatus("PICK A ROOM FIRST: CLICK IT WITH THE ROOM TOOL.");
                return;
            }
            var next = rooms[pickedRoom].Transition == RoomTransition.Slide ? RoomTransition.Cut : RoomTransition.Slide;
            Remember(MapText(grid));
            level.rooms = LevelRooms.SetTransition(level.rooms, pickedRoom, next);
            Commit();
        }

        // ---- Linking ---------------------------------------------------------------------------------------------

        /// <summary>LINK tool: click a plate to pick it, then click doors to link or unlink them to it.</summary>
        void LinkClick(Vector2Int cell)
        {
            char c = grid[cell.y][cell.x];
            if (LevelLinks.IsPlate(c))
            {
                linkPlate = cell;
                SetStatus("PLATE PICKED. CLICK DOORS TO LINK OR UNLINK THEM.");
            }
            else if (LevelLinks.IsDoor(c) && linkPlate.HasValue)
            {
                bool wasLinked = LevelLinks.Contains(level.links, linkPlate.Value, cell);
                Remember(MapText(grid));
                level.links = LevelLinks.Toggle(level.links, linkPlate.Value, cell);
                Commit();
                SetStatus(wasLinked ? "UNLINKED." : "LINKED.");
            }
            else if (LevelLinks.IsDoor(c)) SetStatus("CLICK A PLATE FIRST, THEN ITS DOORS.");
            else linkPlate = null;
        }

        void ShowLinkMark()
        {
            // the picked plate may have been painted over, undone or cut off by a resize
            if (linkPlate is Vector2Int p && (p.x >= W || p.y >= H || !LevelLinks.IsPlate(grid[p.y][p.x]))) linkPlate = null;
            if (tool == Tool.Link && linkPlate.HasValue && !LevelsOpen)
                ShowMarker(ref linkMark, "EditorLinkMark", 29, linkPlate.Value, linkPlate.Value, new Color(1f, 0.71f, 0.28f, 0.5f));
            else Hide(linkMark);
            // the checkpoint whose PAR and TIME are being edited
            if (editing >= 0 && !LevelsOpen && EditedCheckpoint is Vector2Int c)
                ShowMarker(ref checkpointMark, "EditorCheckpointMark", 29, c, c, new Color(0.28f, 0.85f, 1f, 0.45f));
            else Hide(checkpointMark);
        }

        Vector2Int ScreenToCell(Camera cam, Vector2 screen)
        {
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var local = board.transform.InverseTransformPoint(world);
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(-local.y));
        }

        static bool OverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (!es) return false;
            raycastHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screen }, raycastHits);
            return raycastHits.Count > 0;
        }

        // ---- Painting -------------------------------------------------------------------------------------------

        void BeginStroke(Vector2Int cell, bool erase)
        {
            stroking = true;
            erasing = erase;
            strokeStart = strokeLast = cell;
            strokeBase = Copy(grid);
            switch (tool)
            {
                case Tool.Fill: strokeChanged = FloodFill(cell, Active); break;
                case Tool.Rectangle: strokeChanged = ApplyRect(cell, cell, Active); break;
                default:
                    strokeChanged = SetCell(cell, Active);
                    if (strokeChanged) MarkPainted(cell);
                    break;
            }
        }

        // The board is rebuilt when the stroke ends: until then the brush leaves plain squares and the rectangle
        // shows as the cursor.
        void ContinueStroke(Vector2Int cell)
        {
            if (cell == strokeLast || tool == Tool.Fill) return;
            if (tool == Tool.Rectangle)
            {
                Restore(strokeBase);
                ApplyRect(strokeStart, cell, Active);
                strokeChanged = true;
            }
            else
            {
                foreach (var c in Line(strokeLast, cell))
                    if (SetCell(c, Active))
                    {
                        strokeChanged = true;
                        MarkPainted(c);
                    }
            }
            strokeLast = cell;
        }

        void EndStroke()
        {
            stroking = false;
            if (strokeChanged)
            {
                Remember(MapText(strokeBase));
                Commit();
            }
            else ClearStrokeMarks();
            strokeBase = null;
        }

        void MarkPainted(Vector2Int c)
        {
            if (strokeMarksUsed == strokeMarks.Count) strokeMarks.Add(null);
            var mark = strokeMarks[strokeMarksUsed];
            ShowMarker(ref mark, "EditorStroke", 27, c, c, SwatchColor(grid[c.y][c.x], board ? board.Palette : null));
            strokeMarks[strokeMarksUsed++] = mark;
        }

        void ClearStrokeMarks()
        {
            for (int i = 0; i < strokeMarksUsed; i++) Hide(strokeMarks[i]);
            strokeMarksUsed = 0;
        }

        /// <summary>Paints one cell. The start and exit are only ever moved, never painted over.</summary>
        bool SetCell(Vector2Int c, char g)
        {
            if (c.x < 0 || c.y < 0 || c.x >= W || c.y >= H) return false;
            char current = grid[c.y][c.x];
            if (current == g || current == 'S' || current == 'X') return false;
            if (g == 'S' || g == 'X') Replace(grid, g, '.');
            grid[c.y][c.x] = g;
            return true;
        }

        bool ApplyRect(Vector2Int a, Vector2Int b, char g)
        {
            if (g == 'S' || g == 'X') return SetCell(b, g);
            bool changed = false;
            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    changed |= SetCell(new Vector2Int(x, y), g);
            return changed;
        }

        bool FloodFill(Vector2Int start, char g)
        {
            char target = grid[start.y][start.x];
            if (g == 'S' || g == 'X') return SetCell(start, g);
            if (target == g || target == 'S' || target == 'X') return false;
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            bool changed = false;
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.x < 0 || p.y < 0 || p.x >= W || p.y >= H || grid[p.y][p.x] != target) continue;
                grid[p.y][p.x] = g;
                changed = true;
                stack.Push(new Vector2Int(p.x + 1, p.y));
                stack.Push(new Vector2Int(p.x - 1, p.y));
                stack.Push(new Vector2Int(p.x, p.y + 1));
                stack.Push(new Vector2Int(p.x, p.y - 1));
            }
            return changed;
        }

        static IEnumerable<Vector2Int> Line(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, err = dx + dy;
            var p = a;
            while (true)
            {
                yield return p;
                if (p == b) yield break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; p.x += sx; }
                if (e2 <= dx) { err += dx; p.y += sy; }
            }
        }

        // ---- Size and whole-level edits --------------------------------------------------------------------------

        /// <summary>Sets the map to a typed size. Columns and rows come and go just inside the right and bottom walls.</summary>
        void ResizeMap(int w, int h)
        {
            if (level == null) return;
            w = Mathf.Clamp(w, MinWidth, MaxWidth);
            h = Mathf.Clamp(h, MinHeight, MaxHeight);
            if (w == W && h == H) { RefreshInfo(); return; }
            Remember(MapText(grid));
            ApplyEdit(MapEdit.Resize(grid, w, h));
            SetStatus($"THE MAP IS NOW {W} x {H}.");
        }

        /// <summary>Adds solid wall on one side of the canvas, to draw more rooms into.</summary>
        void GrowMap(Vector2Int side)
        {
            if (level == null) return;
            int add = side.x != 0 ? Mathf.Min(GrowStep, MaxWidth - W) : Mathf.Min(GrowStep, MaxHeight - H);
            if (add <= 0)
            {
                SetStatus($"THE CANVAS IS AT MOST {MaxWidth} x {MaxHeight}.");
                return;
            }
            Remember(MapText(grid));
            ApplyEdit(MapEdit.Extend(grid, side.x < 0 ? add : 0, side.y < 0 ? add : 0, side.x > 0 ? add : 0, side.y > 0 ? add : 0));
            // keep what was on screen where it was
            var cam = Camera.main;
            if (cam && (side.x < 0 || side.y < 0)) cam.transform.position += new Vector3(side.x < 0 ? add : 0, side.y < 0 ? -add : 0, 0f);
            SetStatus($"THE CANVAS IS NOW {W} x {H}.");
        }

        /// <summary>Cuts the solid wall around the level down to one layer, keeping every room whole.</summary>
        void TrimMap()
        {
            if (level == null) return;
            var edit = MapEdit.Trim(grid, LevelRooms.Parse(level.rooms).Select(r => r.Cells));
            if (edit.grid.Length == H && edit.grid[0].Length == W)
            {
                SetStatus("NOTHING TO TRIM.");
                return;
            }
            Remember(MapText(grid));
            ApplyEdit(edit);
            fitPending = true;
            SetStatus($"TRIMMED TO {W} x {H}.");
        }

        void ApplyEdit((char[][] grid, Func<Vector2Int, Vector2Int> move) edit)
        {
            grid = edit.grid;
            MoveCells(edit.move);
            EnsureStartAndExit(grid);
            Commit();
        }

        void WallBorder()
        {
            Remember(MapText(grid));
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if ((x == 0 || y == 0 || x == W - 1 || y == H - 1) && grid[y][x] != 'S' && grid[y][x] != 'X') grid[y][x] = '#';
            Commit();
        }

        void ClearLevel()
        {
            Remember(MapText(grid));
            grid = BlankGrid(W, H);
            Commit();
            SetStatus("CLEARED. CTRL+Z OR UNDO BRINGS IT BACK.");
        }

        /// <summary>Moves link ends and checkpoint settings along with their tiles when rows or columns change.</summary>
        void MoveCells(Func<Vector2Int, Vector2Int> move)
        {
            level.links = LevelLinks.Move(level.links, move);
            level.checkpoints = LevelCheckpoints.Move(level.checkpoints, move);
            level.rooms = LevelRooms.Move(level.rooms, move);
        }

        /// <summary>Keeps an undo step: the map as it was before an edit, with the links, checkpoints and rooms of that moment.</summary>
        void Remember(string map)
        {
            undo.Add((map, level.links, level.checkpoints, level.rooms));
            if (undo.Count > UndoLimit) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (undo.Count == 0) { SetStatus("NOTHING TO UNDO."); return; }
            var step = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            grid = ParseGrid(step.map);
            level.links = step.links;
            level.checkpoints = step.checkpoints;
            level.rooms = step.rooms;
            EnsureStartAndExit(grid);
            Commit();
        }

        /// <summary>Keeps the grid in the level and queues a board rebuild. Links, settings and rooms of what is gone are dropped.</summary>
        void Commit()
        {
            level.map = MapText(grid);
            level.links = LevelLinks.Clean(level.links, grid);
            level.checkpoints = LevelCheckpoints.Clean(level.checkpoints, grid);
            level.rooms = LevelRooms.Clean(level.rooms, W, H);
            MarkUnsaved();
            previewDirty = true;
            RefreshInfo();
        }

        /// <summary>Loads the level into the game so the board shows it. Runs once per finished edit, never mid-stroke.</summary>
        void Preview()
        {
            previewDirty = false;
            var game = Game;
            if (!game || grid == null || level == null) return;
            try { LevelLayout.Parse(level.map, level.links, level.checkpoints); }
            catch (FormatException e)
            {
                SetStatus(e.Message.ToUpperInvariant());   // the squares of the last stroke stay, as the board could not show it
                return;
            }
            if (!preview) preview = LevelData.CreateRuntime(level);
            else preview.Configure(level);
            game.LoadCustomLevel(preview);
            if (!board) board = FindFirstObjectByType<BoardView>();
            ClearStrokeMarks();
            if (fitPending)
            {
                fitPending = false;
                FitView();
            }
        }

        // ---- Buttons -------------------------------------------------------------------------------------------------

        void SelectGlyph(char g)
        {
            glyph = g;
            if (tool == Tool.Link || tool == Tool.Room) tool = Tool.Brush;   // picking a tile means painting again
            RefreshSwatches();
            RefreshTools();
            RefreshHint();
        }

        void SetTool(Tool t)
        {
            tool = t;
            RefreshTools();
            RefreshHint();
        }

        void ChangePar(int delta)
        {
            if (level == null) return;
            if (EditedCheckpoint is Vector2Int cell)
            {
                var e = LevelCheckpoints.Get(level.checkpoints, cell);
                level.checkpoints = LevelCheckpoints.Set(level.checkpoints, new LevelCheckpoints.Entry(cell, Mathf.Clamp(e.Par + delta, 1, 20), e.Seconds));
            }
            else level.par = Mathf.Clamp(level.par + delta, 1, 20);
            MarkUnsaved();
            RefreshInfo();
            if (CheckpointCells().Count > 0) SetStatus($"TOTAL PAR {TotalPar}.");
        }

        /// <summary>The level's loop length, or the game's default when it has not set one.</summary>
        float LoopSeconds => level.loopSeconds > 0f ? level.loopSeconds : Game ? Game.Config.LoopSeconds : 10f;

        /// <summary>The checkpoint whose PAR and TIME are being edited, or null for the start.</summary>
        Vector2Int? EditedCheckpoint
        {
            get
            {
                var cells = CheckpointCells();
                return editing >= 0 && editing < cells.Count ? cells[editing] : (Vector2Int?)null;
            }
        }

        List<Vector2Int> CheckpointCells()
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (grid[y][x] == LevelCheckpoints.Glyph) cells.Add(new Vector2Int(x, y));
            return cells;
        }

        int TotalPar
        {
            get
            {
                try { return LevelLayout.Parse(level.map, level.links, level.checkpoints).TotalPar(level.par); }
                catch (FormatException) { return level.par; }
            }
        }

        /// <summary>Steps through the start and each checkpoint, choosing which one PAR and TIME change.</summary>
        void ChangeEditing(int direction)
        {
            if (level == null) return;
            int count = CheckpointCells().Count;
            if (count == 0) { editing = -1; SetStatus("PLACE A CHECKPOINT (+) TO GIVE IT ITS OWN PAR AND TIME."); }
            else editing = (editing + 1 + direction + count + 1) % (count + 1) - 1;   // cycles -1, 0 .. count - 1
            RefreshInfo();
        }

        /// <summary>The loop length of the section being edited; a checkpoint without its own uses the level's.</summary>
        float EditedSeconds
        {
            get
            {
                float s = EditedCheckpoint is Vector2Int cell ? LevelCheckpoints.Get(level.checkpoints, cell).Seconds : 0f;
                return s > 0f ? s : LoopSeconds;
            }
        }

        void ChangeTime(int direction)
        {
            if (level == null) return;
            float seconds = Mathf.Clamp(EditedSeconds + direction * 5f, LevelData.MinLoopSeconds, LevelData.MaxLoopSeconds);
            if (EditedCheckpoint is Vector2Int cell)
                level.checkpoints = LevelCheckpoints.Set(level.checkpoints, new LevelCheckpoints.Entry(cell, LevelCheckpoints.Get(level.checkpoints, cell).Par, seconds));
            else level.loopSeconds = seconds;
            MarkUnsaved();
            RefreshInfo();
        }

        void OnNameEdited(string text)
        {
            if (level == null) return;
            level.name = LevelCode.CleanName(text);
            nameField.SetTextWithoutNotify(level.name);
            MarkUnsaved();
        }

        void TestPlay()
        {
            if (level == null) return;
            Commit();
            FlushSave();
            if (testLevel) Destroy(testLevel);
            testLevel = LevelData.CreateRuntime(level);
            router.PlayCustom(testLevel);
        }

        void CopyCode()
        {
            if (level == null) return;
            level.map = MapText(grid);
            ClipboardBridge.Copy(LevelCode.Encode(level));
            SetStatus("LEVEL CODE COPIED.");
        }

        void PasteCode()
        {
            string text = ClipboardBridge.Paste("Paste a Rewind level code:");
            if (LevelCode.TryDecode(text, out var imported, out string error))
            {
                CustomLevelStore.Save(imported);
                OpenLevel(imported);
                SetStatus($"IMPORTED \"{imported.name.ToUpperInvariant()}\".");
            }
            else if (string.IsNullOrWhiteSpace(text)) SetStatus("NOTHING TO PASTE. COPY A LEVEL CODE FIRST.");
            else SetStatus("THAT IS NOT A LEVEL CODE: " + error.ToUpperInvariant());
        }

        void NewLevel()
        {
            OpenLevel(CreateLevel(NewWidth, NewHeight));
            ShowLevels(false);
            SetStatus("NEW LEVEL.");
        }

        void ShowLevels(bool show)
        {
            if (!levelsPanel) return;
            pendingDelete = null;
            levelsPanel.gameObject.SetActive(show);
            if (!show) return;
            HideCursor();
            page = 0;
            RefreshLevels();
        }

        void TurnPage(int delta)
        {
            page += delta;
            RefreshLevels();
        }

        void DeleteLevel(CustomLevel rec)
        {
            if (pendingDelete != rec.id)
            {
                pendingDelete = rec.id;   // a second press confirms
                RefreshLevels();
                return;
            }
            pendingDelete = null;
            CustomLevelStore.Delete(rec.id);
            if (level != null && level.id == rec.id)
            {
                saveAt = -1f;   // a pending save would bring the deleted level back
                var next = CustomLevelStore.All.OrderByDescending(l => l.updatedTicks).FirstOrDefault();
                OpenLevel(next ?? CreateLevel(NewWidth, NewHeight));
            }
            RefreshLevels();
        }

        void OpenLevel(CustomLevel rec)
        {
            FlushSave();   // the level being left
            level = rec;
            grid = ParseGrid(rec.map);
            EnsureStartAndExit(grid);
            // levels from before links existed join plates and doors by colour: rewrite them as p/d/D with links, once
            string links = LevelLinks.Clean(LevelLinks.ConvertColourLinks(grid, rec.links), grid), map = MapText(grid);
            if (map != rec.map || links != rec.links)
            {
                rec.map = map;
                rec.links = links;
                rec.checkpoints = LevelCheckpoints.Clean(rec.checkpoints, grid);
                CustomLevelStore.Save(rec);
            }
            undo.Clear();
            linkPlate = null;
            editing = pickedRoom = -1;
            pendingDelete = null;
            CustomLevelStore.LastEditedId = rec.id;
            if (nameField) nameField.SetTextWithoutNotify(rec.name);
            previewDirty = fitPending = true;
            RefreshAll();
        }

        // ---- Views ---------------------------------------------------------------------------------------------------

        void RefreshLevels()
        {
            for (int i = levelsList.childCount - 1; i >= 0; i--)
            {
                var child = levelsList.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            var all = CustomLevelStore.All.OrderByDescending(l => l.updatedTicks).ToList();
            int pages = Mathf.Max(1, Mathf.CeilToInt(all.Count / (float)RowsPerPage));
            page = Mathf.Clamp(page, 0, pages - 1);
            pageLabel.text = $"{page + 1} / {pages}";
            if (all.Count == 0)
            {
                RuntimeUi.Size(RuntimeUi.Label(levelsList, "Empty", "NO LEVELS YET.", 26f, RuntimeUi.Muted, TextAlignmentOptions.MidlineLeft), -1f, 64f);
                return;
            }
            foreach (var rec in all.Skip(page * RowsPerPage).Take(RowsPerPage)) BuildLevelRow(rec);
        }

        void BuildLevelRow(CustomLevel rec)
        {
            bool current = level != null && rec.id == level.id;
            var row = RuntimeUi.Rect("Row", levelsList);
            RuntimeUi.Fill(row, RuntimeUi.WithAlpha(RuntimeUi.Deck, current ? 1f : 0.45f), false);
            RuntimeUi.Row(row, 10f).padding = new RectOffset(16, 8, 6, 6);
            RuntimeUi.Size(row, -1f, 64f);

            var name = RuntimeUi.Label(row, "Name", rec.name.ToUpperInvariant(), 28f, current ? RuntimeUi.Cyan : RuntimeUi.Fg, TextAlignmentOptions.MidlineLeft);
            RuntimeUi.Size(name, -1f, 52f);
            name.GetComponent<LayoutElement>().flexibleWidth = 1f;
            string solved = rec.bestLoops > 0 ? $"SOLVED IN {rec.bestLoops}" : "UNSOLVED";
            var info = RuntimeUi.Label(row, "Info", $"{Dimensions(rec.map)}   PAR {rec.par}   {solved}", 22f, RuntimeUi.Muted, TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            RuntimeUi.Size(info, 380f, 52f);
            RuntimeUi.Button(row, "PLAY", 110f, 52f, () => { OpenLevel(rec); ShowLevels(false); TestPlay(); });
            RuntimeUi.Button(row, "EDIT", 110f, 52f, () => { OpenLevel(rec); ShowLevels(false); });
            RuntimeUi.Button(row, pendingDelete == rec.id ? "SURE?" : "DELETE", 130f, 52f, () => DeleteLevel(rec));
        }

        void RefreshAll()
        {
            RefreshInfo();
            RefreshSwatches();
            RefreshTools();
            RefreshHint();
        }

        void RefreshInfo()
        {
            if (level == null || grid == null) return;
            var checkpoint = EditedCheckpoint;
            if (checkpoint == null) editing = -1;   // its checkpoint was painted over
            if (parLabel) parLabel.text = (checkpoint is Vector2Int c ? LevelCheckpoints.Get(level.checkpoints, c).Par : level.par).ToString();
            if (editingLabel) editingLabel.text = editing < 0 ? "START" : $"CHECKPOINT {editing + 1}";
            if (timeLabel) timeLabel.text = $"{EditedSeconds:0.#} SEC";
            ShowNumber(mapWidthField, W);
            ShowNumber(mapHeightField, H);
            var rooms = LevelRooms.Parse(level.rooms);
            if (pickedRoom >= rooms.Count) pickedRoom = -1;
            if (roomLabel)
                roomLabel.text = rooms.Count == 0 ? "NO ROOMS: ONE BIG ROOM"
                    : pickedRoom < 0 ? $"{rooms.Count} ROOM{(rooms.Count == 1 ? "" : "S")}"
                    : $"ROOM {pickedRoom + 1}: {rooms[pickedRoom].Cells.width} x {rooms[pickedRoom].Cells.height}";
            if (transitionLabel)
                transitionLabel.text = pickedRoom < 0 ? "CAMERA: -" : rooms[pickedRoom].Transition == RoomTransition.Cut ? "CAMERA: CUT" : "CAMERA: SLIDE";
            ShowRoomOutlines(rooms);
        }

        static void ShowNumber(TMP_InputField field, int value)
        {
            if (field && !field.isFocused) field.SetTextWithoutNotify(value.ToString());   // never under the player's typing
        }

        // outlines every room, the picked one brighter, so you can see what the camera will frame
        void ShowRoomOutlines(List<Room> rooms)
        {
            const float t = 0.14f;
            int used = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i].Cells;
                var tint = i == pickedRoom ? new Color(1f, 0.71f, 0.28f, 0.9f) : new Color(0.28f, 0.85f, 1f, 0.6f);
                ShowLine(used++, new Vector3(r.x + r.width * 0.5f, -r.y), new Vector3(r.width, t), tint);
                ShowLine(used++, new Vector3(r.x + r.width * 0.5f, -r.yMax), new Vector3(r.width, t), tint);
                ShowLine(used++, new Vector3(r.x, -(r.y + r.height * 0.5f)), new Vector3(t, r.height), tint);
                ShowLine(used++, new Vector3(r.xMax, -(r.y + r.height * 0.5f)), new Vector3(t, r.height), tint);
            }
            for (int k = used; k < roomLines.Count; k++) Hide(roomLines[k]);
        }

        void ShowLine(int index, Vector3 localCentre, Vector3 localSize, Color tint)
        {
            while (roomLines.Count <= index) roomLines.Add(null);
            var line = roomLines[index];
            if (!line && !(line = MakeQuad("EditorRoomLine", 28))) return;
            line.transform.localPosition = localCentre;
            line.transform.localScale = new Vector3(localSize.x, localSize.y, 1f);
            line.color = tint;
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
            roomLines[index] = line;
        }

        void RefreshSwatches()
        {
            foreach (var v in swatchViews) v.frame.color = v.glyph == glyph ? RuntimeUi.Rec : Color.clear;
        }

        void RefreshTools()
        {
            for (int i = 0; i < toolImages.Length; i++)
                if (toolImages[i]) toolImages[i].color = i == (int)tool ? ActiveTool : RuntimeUi.Deck;
        }

        void RefreshHint()
        {
            if (!hintLabel) return;
            const string view = "WHEEL OR PINCH ZOOMS   |   MIDDLE DRAG OR SPACE + DRAG PANS";
            if (tool == Tool.Link)
            {
                hintLabel.text = "LINK: CLICK A PLATE, THEN CLICK DOORS TO LINK OR UNLINK THEM   |   RIGHT CLICK DROPS THE PLATE   |   " + view;
                return;
            }
            if (tool == Tool.Room)
            {
                hintLabel.text = "ROOM: DRAG TO DRAW A ROOM (IN SOLID WALL IT IS CARVED OUT)   |   CLICK PICKS ONE   |   RIGHT CLICK REMOVES   |   " + view;
                return;
            }
            string name = Swatches.First(s => s.Glyph == glyph).Name.ToUpperInvariant();
            hintLabel.text = $"PAINTING: {name}   |   {ToolNames[(int)tool]}   |   RIGHT CLICK ERASES   |   CTRL+Z UNDOES   |   {view}";
        }

        void RecolorSwatches()
        {
            var palette = board ? board.Palette : null;
            foreach (var v in swatchViews)
            {
                var c = SwatchColor(v.glyph, palette);
                v.fill.color = c;
                v.text.color = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f > 0.5f ? RuntimeUi.Ink : RuntimeUi.Fg;
            }
        }

        static Color SwatchColor(char c, BoardPalette p)
        {
            var plate = p ? p.Channel(1) : RuntimeUi.Amber;   // on the board, linked tiles get their group's colour
            switch (c)
            {
                case '#': return p ? p.wall : new Color(0.16f, 0.18f, 0.36f);
                case '.': return p ? p.floorA : new Color(0.07f, 0.08f, 0.17f);
                case 'S': return p ? p.player : Color.white;
                case 'X': return p ? p.exit : RuntimeUi.Exit;
                case 'o': return p ? p.crate : new Color(0.79f, 0.55f, 0.31f);
                case '~': return p ? p.crack : new Color(0.49f, 0.51f, 0.7f);
                case '_': return p ? p.pit : new Color(0.02f, 0.02f, 0.05f);
                case 'p': return Color.Lerp(plate, Color.black, 0.45f);
                case 'd': return plate;
                case 'D': return Color.Lerp(plate, Color.black, 0.6f);
                case LevelCheckpoints.Glyph: return p ? p.rewind : RuntimeUi.Cyan;
                default: return Color.gray;
            }
        }

        void SetStatus(string text, float seconds = StatusSeconds)
        {
            if (!statusLabel) return;
            statusLabel.text = text;
            statusUntil = Time.unscaledTime + seconds;
        }

        void ShowCursor(Vector2Int a, Vector2Int b, bool erase) =>
            ShowMarker(ref cursor, "EditorCursor", 30, a, b, erase ? new Color(1f, 0.3f, 0.35f, 0.32f) : new Color(1f, 1f, 1f, 0.2f));

        void HideCursor() => Hide(cursor);

        /// <summary>Shows a tinted rectangle over the cells from a to b on the board, creating it the first time.</summary>
        void ShowMarker(ref SpriteRenderer marker, string name, int order, Vector2Int a, Vector2Int b, Color color)
        {
            if (!marker && !(marker = MakeQuad(name, order))) return;
            var min = Vector2Int.Min(a, b);
            var size = Vector2Int.Max(a, b) - min + Vector2Int.one;
            marker.transform.localPosition = new Vector3(min.x + size.x * 0.5f, -(min.y + size.y * 0.5f), 0f);
            marker.transform.localScale = new Vector3(size.x, size.y, 1f);
            marker.color = color;
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        }

        /// <summary>A plain white square on the board, one unit across, for markers and outlines. Null without a board.</summary>
        SpriteRenderer MakeQuad(string name, int order)
        {
            if (!board) return null;
            var template = board.GetComponentInChildren<SpriteRenderer>(true);
            var go = new GameObject(name);
            go.transform.SetParent(board.transform, false);
            var quad = go.AddComponent<SpriteRenderer>();
            quad.sprite = WhiteSprite();
            quad.sortingOrder = order;
            if (template) quad.sharedMaterial = template.sharedMaterial;
            return quad;
        }

        static void Hide(SpriteRenderer marker)
        {
            if (marker && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }

        static Sprite WhiteSprite()
        {
            if (whiteSprite) return whiteSprite;
            var tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            return whiteSprite;
        }

        // ---- Grid helpers ----------------------------------------------------------------------------------------------

        static CustomLevel CreateLevel(int w, int h)
        {
            var rec = new CustomLevel { name = $"Untitled {CustomLevelStore.All.Count + 1}", par = 2, map = MapText(BlankGrid(w, h)) };
            CustomLevelStore.Save(rec);
            return rec;
        }

        static char[][] BlankGrid(int w, int h)
        {
            var g = new char[h][];
            for (int y = 0; y < h; y++)
            {
                g[y] = new char[w];
                for (int x = 0; x < w; x++) g[y][x] = x == 0 || y == 0 || x == w - 1 || y == h - 1 ? '#' : '.';
            }
            g[h / 2][1] = 'S';
            g[h / 2][w - 2] = 'X';
            return g;
        }

        static char[][] ParseGrid(string map)
        {
            var rows = LevelLayout.SplitRows(map);
            if (rows.Count == 0) return BlankGrid(NewWidth, NewHeight);
            int w = Mathf.Clamp(rows.Max(r => r.Length), MinWidth, MaxWidth);
            int h = Mathf.Clamp(rows.Count, MinHeight, MaxHeight);
            var g = new char[h][];
            for (int y = 0; y < h; y++)
            {
                g[y] = new char[w];
                for (int x = 0; x < w; x++)
                {
                    char c = y < rows.Count && x < rows[y].Length ? rows[y][x] : '.';
                    g[y][x] = IsKnown(c) ? c : '.';
                }
            }
            return g;
        }

        static bool IsKnown(char c)
        {
            foreach (var s in Swatches) if (s.Glyph == c) return true;
            return LevelLinks.ColourOf(c) > 0;   // older colour-linked tiles, converted by OpenLevel
        }

        static string MapText(char[][] g) => string.Join("\n", g.Select(r => new string(r)));

        static string Dimensions(string map)
        {
            var rows = LevelLayout.SplitRows(map);
            return rows.Count == 0 ? "EMPTY" : $"{rows.Max(r => r.Length)} x {rows.Count}";
        }

        static char[][] Copy(char[][] g) => g.Select(r => (char[])r.Clone()).ToArray();

        void Restore(char[][] from)
        {
            for (int y = 0; y < H; y++) Array.Copy(from[y], grid[y], W);
        }

        static void Replace(char[][] g, char from, char to)
        {
            foreach (var row in g)
                for (int x = 0; x < row.Length; x++)
                    if (row[x] == from) row[x] = to;
        }

        /// <summary>A level always has exactly one start and one exit, so it can always be played.</summary>
        static void EnsureStartAndExit(char[][] g)
        {
            KeepFirst(g, 'S');
            KeepFirst(g, 'X');
            if (!Contains(g, 'S')) Place(g, 'S', fromEnd: false);
            if (!Contains(g, 'X')) Place(g, 'X', fromEnd: true);
        }

        static void KeepFirst(char[][] g, char glyph)
        {
            bool seen = false;
            foreach (var row in g)
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] != glyph) continue;
                    if (seen) row[x] = '.';
                    seen = true;
                }
        }

        static bool Contains(char[][] g, char glyph) => g.Any(row => Array.IndexOf(row, glyph) >= 0);

        static void Place(char[][] g, char glyph, bool fromEnd)
        {
            int h = g.Length, w = g[0].Length;
            for (int i = 0; i < w * h; i++)
            {
                int k = fromEnd ? w * h - 1 - i : i;
                int x = k % w, y = k / w;
                if (g[y][x] != '.') continue;
                g[y][x] = glyph;
                return;
            }
            g[h / 2][fromEnd ? w - 2 : 1] = glyph;   // no floor left at all: carve a spot
        }
    }
}
