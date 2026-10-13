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
        const int MinWidth = 5, MinHeight = 4, MaxWidth = 60, MaxHeight = 36;
        const int NewWidth = 20, NewHeight = 11, UndoLimit = 80, RowsPerPage = 6;
        const float StatusSeconds = 4f;

        enum Tool { Brush, Rectangle, Fill }

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
            new Swatch('1', "Amber plate"), new Swatch('2', "Cyan plate"), new Swatch('3', "Orange plate"),
            new Swatch('a', "Amber door, open while held"), new Swatch('b', "Cyan door, open while held"), new Swatch('c', "Orange door, open while held"),
            new Swatch('A', "Amber door, shut while held"), new Swatch('B', "Cyan door, shut while held"), new Swatch('C', "Orange door, shut while held"),
        };

        static readonly string[] ToolNames = { "BRUSH", "RECT", "FILL" };
        static readonly Color ActiveTool = new Color(0.5f, 0.15f, 0.2f);
        static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
        static Sprite whiteSprite;

        // what is being edited
        CustomLevel level;
        char[][] grid;
        readonly List<string> undo = new List<string>();
        char glyph = '#';
        Tool tool = Tool.Brush;

        // painting
        bool stroking, erasing, strokeChanged, previewDirty;
        Vector2Int strokeStart, strokeLast;
        char[][] strokeBase;
        LevelData preview, testLevel;
        BoardView board;
        SpriteRenderer cursor;

        // ui
        TMP_InputField nameField;
        TextMeshProUGUI parLabel, sizeLabel, statusLabel, hintLabel, pageLabel;
        readonly List<(Image fill, Image frame, TextMeshProUGUI text, char glyph)> swatchViews = new List<(Image, Image, TextMeshProUGUI, char)>();
        readonly Image[] toolImages = new Image[3];
        RectTransform levelsPanel, levelsList;
        int page;
        string pendingDelete, solveNote;
        float statusUntil = -1f, nextStrokePreview;

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
            panel.offsetMax = new Vector2(-24f, 240f);
            RuntimeUi.Fill(panel, RuntimeUi.WithAlpha(RuntimeUi.Bezel, 0.94f), true);
            RuntimeUi.Column(panel, 10f, new RectOffset(20, 20, 16, 14));

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

            // size
            var size = RuntimeUi.Rect("Size", panel);
            RuntimeUi.Row(size, 8f);
            RuntimeUi.Size(size, -1f, 56f);
            sizeLabel = RuntimeUi.Label(size, "SizeLabel", "", 26f, RuntimeUi.Fg, TextAlignmentOptions.MidlineLeft);
            RuntimeUi.Size(sizeLabel, 150f, 56f);
            RuntimeUi.Button(size, "+ COLUMN", 160f, 52f, AddColumn);
            RuntimeUi.Button(size, "- COLUMN", 160f, 52f, RemoveColumn);
            RuntimeUi.Button(size, "+ ROW", 130f, 52f, AddRow);
            RuntimeUi.Button(size, "- ROW", 130f, 52f, RemoveRow);
            RuntimeUi.Button(size, "WALL BORDER", 190f, 52f, WallBorder);
            RuntimeUi.Button(size, "CLEAR", 120f, 52f, ClearLevel);
            RuntimeUi.Flexible(size);
            statusLabel = RuntimeUi.Label(size, "Status", "", 24f, RuntimeUi.Amber, TextAlignmentOptions.MidlineRight);
            RuntimeUi.Size(statusLabel, 560f, 56f);

            hintLabel = RuntimeUi.Label(panel, "Hint", "", 21f, RuntimeUi.Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            RuntimeUi.Size(hintLabel, -1f, 30f);
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
            ShowLevels(false);
            if (level == null) OpenLevel(CustomLevelStore.Get(CustomLevelStore.LastEditedId) ?? CreateLevel(NewWidth, NewHeight));
            else
            {
                previewDirty = true;
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
            stroking = false;
            strokeBase = null;
            HideCursor();
        }

        void OnDestroy()
        {
            if (preview) Destroy(preview);
            if (testLevel) Destroy(testLevel);
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
            solveNote = loops == level.par ? $"SOLVED AT PAR: {loops} LOOPS."
                : loops < level.par ? $"SOLVED IN {loops} LOOPS, UNDER PAR {level.par}. LOWER THE PAR?"
                : $"SOLVED IN {loops} LOOPS, OVER PAR {level.par}.";
        }

        protected override void OnTick()
        {
            if (grid == null) return;
            if (LevelsOpen) HideCursor();
            else
            {
                HandleKeys();
                HandlePointer();
            }
            // Rebuilding the board is the expensive part: on big maps, refresh it a few times a second while dragging.
            if (previewDirty && (!stroking || W * H <= 400 || Time.unscaledTime >= nextStrokePreview))
            {
                Preview();
                nextStrokePreview = Time.unscaledTime + 0.1f;
            }
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

            if (stroking)
            {
                var clamped = new Vector2Int(Mathf.Clamp(cell.x, 0, W - 1), Mathf.Clamp(cell.y, 0, H - 1));
                // only the rectangle snaps to the board's edge; a brush dragged off the board paints nothing out there
                if (leftHeld || rightHeld) ContinueStroke(tool == Tool.Rectangle ? clamped : cell);
                else EndStroke();
                if (stroking) ShowCursor(tool == Tool.Rectangle ? strokeStart : clamped, clamped, erasing);
                else HideCursor();
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
            if (leftDown || rightDown) BeginStroke(cell, rightDown && !leftDown);
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
                default: strokeChanged = SetCell(cell, Active); break;
            }
            if (strokeChanged) previewDirty = true;
        }

        void ContinueStroke(Vector2Int cell)
        {
            if (cell == strokeLast || tool == Tool.Fill) return;
            if (tool == Tool.Rectangle)
            {
                Restore(strokeBase);
                ApplyRect(strokeStart, cell, Active);
                strokeChanged = previewDirty = true;
            }
            else
            {
                foreach (var c in Line(strokeLast, cell))
                    if (SetCell(c, Active)) strokeChanged = previewDirty = true;
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
            strokeBase = null;
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

        void AddColumn()
        {
            if (W >= MaxWidth) { SetStatus("THAT IS AS WIDE AS IT GETS."); return; }
            Remember(MapText(grid));
            int at = W - 1;   // just inside the right edge, so an outer wall stays on the edge
            for (int y = 0; y < H; y++)
            {
                var row = grid[y];
                var wider = new char[row.Length + 1];
                Array.Copy(row, 0, wider, 0, at);
                wider[at] = (y == 0 || y == H - 1) && row[at] == '#' ? '#' : '.';
                Array.Copy(row, at, wider, at + 1, row.Length - at);
                grid[y] = wider;
            }
            Commit();
        }

        void RemoveColumn()
        {
            if (W <= MinWidth) { SetStatus("THAT IS AS NARROW AS IT GETS."); return; }
            Remember(MapText(grid));
            int at = W - 2;
            for (int y = 0; y < H; y++)
            {
                var row = grid[y];
                var narrower = new char[row.Length - 1];
                Array.Copy(row, 0, narrower, 0, at);
                Array.Copy(row, at + 1, narrower, at, row.Length - at - 1);
                grid[y] = narrower;
            }
            EnsureStartAndExit(grid);
            Commit();
        }

        void AddRow()
        {
            if (H >= MaxHeight) { SetStatus("THAT IS AS TALL AS IT GETS."); return; }
            Remember(MapText(grid));
            int at = H - 1;
            var row = new char[W];
            for (int x = 0; x < W; x++) row[x] = (x == 0 || x == W - 1) && grid[at][x] == '#' ? '#' : '.';
            var rows = grid.ToList();
            rows.Insert(at, row);
            grid = rows.ToArray();
            Commit();
        }

        void RemoveRow()
        {
            if (H <= MinHeight) { SetStatus("THAT IS AS SHORT AS IT GETS."); return; }
            Remember(MapText(grid));
            var rows = grid.ToList();
            rows.RemoveAt(H - 2);
            grid = rows.ToArray();
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

        void Remember(string map)
        {
            undo.Add(map);
            if (undo.Count > UndoLimit) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (undo.Count == 0) { SetStatus("NOTHING TO UNDO."); return; }
            grid = ParseGrid(undo[undo.Count - 1]);
            undo.RemoveAt(undo.Count - 1);
            EnsureStartAndExit(grid);
            Commit();
        }

        /// <summary>Saves the grid to the level and reloads the board.</summary>
        void Commit()
        {
            level.map = MapText(grid);
            CustomLevelStore.Save(level);
            previewDirty = true;
            RefreshInfo();
        }

        void Preview()
        {
            previewDirty = false;
            var game = Game;
            if (!game || grid == null || level == null) return;
            string map = MapText(grid);
            try { LevelLayout.Parse(map); }
            catch (FormatException e)
            {
                SetStatus(e.Message.ToUpperInvariant());
                return;
            }
            if (!preview) preview = LevelData.CreateRuntime(level.name, level.par, map);
            else preview.Configure(level.name, level.par, map);
            game.LoadCustomLevel(preview);
            if (!board) board = FindFirstObjectByType<BoardView>();
        }

        // ---- Buttons -------------------------------------------------------------------------------------------------

        void SelectGlyph(char g)
        {
            glyph = g;
            RefreshSwatches();
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
            level.par = Mathf.Clamp(level.par + delta, 1, 20);
            CustomLevelStore.Save(level);
            RefreshInfo();
        }

        void OnNameEdited(string text)
        {
            if (level == null) return;
            level.name = LevelCode.CleanName(text);
            nameField.SetTextWithoutNotify(level.name);
            CustomLevelStore.Save(level);
        }

        void TestPlay()
        {
            if (level == null) return;
            Commit();
            if (testLevel) Destroy(testLevel);
            testLevel = LevelData.CreateRuntime(level.name, level.par, level.map);
            router.PlayCustom(testLevel);
        }

        void CopyCode()
        {
            if (level == null) return;
            ClipboardBridge.Copy(LevelCode.Encode(level.name, level.par, MapText(grid)));
            SetStatus("LEVEL CODE COPIED.");
        }

        void PasteCode()
        {
            string text = ClipboardBridge.Paste("Paste a Rewind level code:");
            if (LevelCode.TryDecode(text, out string name, out int par, out string map, out string error))
            {
                var imported = new CustomLevel { name = name, par = par, map = map };
                CustomLevelStore.Save(imported);
                OpenLevel(imported);
                SetStatus($"IMPORTED \"{name.ToUpperInvariant()}\".");
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
                var next = CustomLevelStore.All.OrderByDescending(l => l.updatedTicks).FirstOrDefault();
                OpenLevel(next ?? CreateLevel(NewWidth, NewHeight));
            }
            RefreshLevels();
        }

        void OpenLevel(CustomLevel rec)
        {
            level = rec;
            grid = ParseGrid(rec.map);
            EnsureStartAndExit(grid);
            undo.Clear();
            pendingDelete = null;
            CustomLevelStore.LastEditedId = rec.id;
            if (nameField) nameField.SetTextWithoutNotify(rec.name);
            previewDirty = true;
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
            if (parLabel) parLabel.text = level.par.ToString();
            if (sizeLabel) sizeLabel.text = $"{W} x {H}";
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
            string name = Swatches.First(s => s.Glyph == glyph).Name.ToUpperInvariant();
            hintLabel.text = $"PAINTING: {name}   |   {ToolNames[(int)tool]}   |   CLICK OR DRAG ON THE BOARD, RIGHT CLICK ERASES   |   CTRL+Z UNDOES   |   START AND EXIT CAN ONLY BE MOVED";
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
            Color Channel(int i) => p ? p.Channel(i) : i == 1 ? RuntimeUi.Amber : i == 2 ? RuntimeUi.Cyan : new Color(1f, 0.54f, 0.36f);
            switch (c)
            {
                case '#': return p ? p.wall : new Color(0.16f, 0.18f, 0.36f);
                case '.': return p ? p.floorA : new Color(0.07f, 0.08f, 0.17f);
                case 'S': return p ? p.player : Color.white;
                case 'X': return p ? p.exit : RuntimeUi.Exit;
                case 'o': return p ? p.crate : new Color(0.79f, 0.55f, 0.31f);
                case '~': return p ? p.crack : new Color(0.49f, 0.51f, 0.7f);
                case '_': return p ? p.pit : new Color(0.02f, 0.02f, 0.05f);
                case '1': case '2': case '3': return Color.Lerp(Channel(c - '0'), Color.black, 0.45f);
                case 'a': case 'b': case 'c': return Channel(c - 'a' + 1);
                case 'A': case 'B': case 'C': return Color.Lerp(Channel(c - 'A' + 1), Color.black, 0.6f);
                default: return Color.gray;
            }
        }

        void SetStatus(string text, float seconds = StatusSeconds)
        {
            if (!statusLabel) return;
            statusLabel.text = text;
            statusUntil = Time.unscaledTime + seconds;
        }

        void ShowCursor(Vector2Int a, Vector2Int b, bool erase)
        {
            if (!board) return;
            if (!cursor)
            {
                var template = board.GetComponentInChildren<SpriteRenderer>(true);
                var go = new GameObject("EditorCursor");
                go.transform.SetParent(board.transform, false);
                cursor = go.AddComponent<SpriteRenderer>();
                cursor.sprite = WhiteSprite();
                cursor.sortingOrder = 30;
                if (template) cursor.sharedMaterial = template.sharedMaterial;
            }
            var min = Vector2Int.Min(a, b);
            var size = Vector2Int.Max(a, b) - min + Vector2Int.one;
            cursor.transform.localPosition = new Vector3(min.x + size.x * 0.5f, -(min.y + size.y * 0.5f), 0f);
            cursor.transform.localScale = new Vector3(size.x, size.y, 1f);
            cursor.color = erase ? new Color(1f, 0.3f, 0.35f, 0.32f) : new Color(1f, 1f, 1f, 0.2f);
            if (!cursor.gameObject.activeSelf) cursor.gameObject.SetActive(true);
        }

        void HideCursor()
        {
            if (cursor && cursor.gameObject.activeSelf) cursor.gameObject.SetActive(false);
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
            return false;
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
