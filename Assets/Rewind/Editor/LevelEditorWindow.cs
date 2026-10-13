using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Rewind.Core;
using Rewind.Data;
using Rewind.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rewind.EditorTools
{
    /// <summary>
    /// Paint levels on a grid instead of typing the text map. It reads and writes the same LevelData assets,
    /// uses the game's own parser, simulation and validator, and can playtest the level in the Main scene.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        const string LevelFolder = "Assets/Rewind/Data/Levels";
        const string MainScene = "Assets/Rewind/Scenes/Main.unity";
        const int MinSize = 3, MaxSize = 100;
        const float SideWidth = 320f;

        enum Tool { Brush, Rectangle, Box, Fill }

        struct Entry
        {
            public readonly char Glyph;
            public readonly string Name;
            public Entry(char glyph, string name) { Glyph = glyph; Name = name; }
        }

        static readonly Entry[] Palette =
        {
            new Entry('.', "Floor"), new Entry('#', "Wall"), new Entry('S', "Start"), new Entry('X', "Exit"), new Entry('o', "Crate"),
            new Entry('~', "Cracked floor (gives way once left)"), new Entry('_', "Hole (a crate pushed in fills it)"),
            new Entry('1', "Plate, amber"), new Entry('2', "Plate, cyan"), new Entry('3', "Plate, orange"),
            new Entry('a', "Door, amber (open while held)"), new Entry('b', "Door, cyan (open while held)"), new Entry('c', "Door, orange (open while held)"),
            new Entry('A', "Door, amber (shut while held)"), new Entry('B', "Door, cyan (shut while held)"), new Entry('C', "Door, orange (shut while held)"),
        };

        static readonly string[] ChannelNames = { "", "amber", "cyan", "orange" };

        // serialized so the window survives a domain reload
        [SerializeField] LevelData level;
        [SerializeField] float cell = 30f;
        [SerializeField] bool showPaths = true;
        [SerializeField] string newName = "New Level";
        [SerializeField] int newWidth = 24, newHeight = 14;

        SerializedObject so;
        char[][] grid;
        string lastMap;
        Tool tool = Tool.Brush;
        char glyph = '#';
        Vector2 gridScroll, sideScroll;
        Vector2Int hover = new Vector2Int(-1, -1);

        // stroke state
        bool erasing, strokeChanged;
        Vector2Int strokeStart, strokeLast;
        char[][] strokeSnapshot;

        // analysis
        readonly List<(MessageType type, string text)> checks = new List<(MessageType, string)>();
        string validation;
        bool validationOk;
        string solverNote;

        // solution overlay
        string pathKey;
        readonly List<List<Vector2Int>> paths = new List<List<Vector2Int>>();
        readonly List<Vector2Int> paradoxCells = new List<Vector2Int>();
        BoardPalette boardPalette;
        GUIStyle cellStyle;

        int W => grid != null && grid.Length > 0 ? grid[0].Length : 0;
        int H => grid != null ? grid.Length : 0;

        [MenuItem("Rewind/Level Editor", priority = 10)]
        public static void Open() => OpenWith(null);

        public static void OpenWith(LevelData target)
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(900, 560);
            if (target) window.SetLevel(target);
            window.Show();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
            if (level) SetLevel(level);
        }

        void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        void OnUndoRedo()
        {
            if (level) Load();
            Repaint();
        }

        void SetLevel(LevelData value)
        {
            level = value;
            so = level ? new SerializedObject(level) : null;
            validation = solverNote = null;
            pathKey = null;
            if (level) Load();
            else grid = null;
            Repaint();
        }

        // ---- Map <-> grid ---------------------------------------------------------------------------------

        void Load()
        {
            so = new SerializedObject(level);
            var rows = LevelLayout.SplitRows(level.Map);
            int w = Mathf.Max(MinSize, rows.Count == 0 ? MinSize : rows.Max(r => r.Length));
            int h = Mathf.Max(MinSize, rows.Count);
            grid = new char[h][];
            for (int y = 0; y < h; y++)
            {
                grid[y] = new char[w];
                for (int x = 0; x < w; x++)
                {
                    char c = y < rows.Count && x < rows[y].Length ? rows[y][x] : '.';
                    grid[y][x] = Palette.Any(p => p.Glyph == c) ? c : '.';
                }
            }
            lastMap = level.Map;
            pathKey = null;
            Analyze();
        }

        string GridToMap() => string.Join("\n", grid.Select(r => new string(r)));

        void Save()
        {
            if (!level || grid == null) return;
            so.Update();
            so.FindProperty("map").stringValue = GridToMap();
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(level);
            lastMap = level.Map;
            pathKey = null;
            Analyze();
        }

        // ---- GUI --------------------------------------------------------------------------------------------

        void OnGUI()
        {
            cellStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            DrawTopBar();

            if (!level)
            {
                EditorGUILayout.HelpBox("Pick a level above, or create one.", MessageType.Info);
                DrawNewLevel();
                return;
            }
            if (so == null || grid == null || level.Map != lastMap) Load();
            so.Update();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGridArea();
                using (var scroll = new EditorGUILayout.ScrollViewScope(sideScroll, GUILayout.Width(SideWidth)))
                {
                    sideScroll = scroll.scrollPosition;
                    DrawSidePanel();
                }
            }
            so.ApplyModifiedProperties();
        }

        void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (LevelData)EditorGUILayout.ObjectField(level, typeof(LevelData), false, GUILayout.Width(220));
                if (picked != level) SetLevel(picked);

                var catalog = FindCatalog();
                if (catalog && catalog.Count > 0)
                {
                    var names = catalog.Levels.Select((l, i) => l ? $"{i + 1:00}  {l.DisplayName}" : $"{i + 1:00}  (missing)").ToArray();
                    int current = level ? catalog.IndexOf(level) : -1;
                    int chosen = EditorGUILayout.Popup(current, names, EditorStyles.toolbarPopup, GUILayout.Width(190));
                    if (chosen != current && chosen >= 0 && catalog[chosen]) SetLevel(catalog[chosen]);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Import code", "Make a new level from a level code on the clipboard, e.g. one copied in the game's level editor."), EditorStyles.toolbarButton))
                    ImportCode();
                using (new EditorGUI.DisabledScope(!level))
                {
                    if (GUILayout.Button(new GUIContent("Copy code", "Copy this level as a one-line code that the in-game editor can paste."), EditorStyles.toolbarButton))
                        CopyCode();
                    if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton)) Duplicate();
                    if (GUILayout.Button("Ping asset", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(level);
                    if (GUILayout.Button("▶ Playtest", EditorStyles.toolbarButton)) { Save(); LevelPlaytest.Start(level); }
                }
            }
        }

        void DrawNewLevel()
        {
            EditorGUILayout.LabelField("New level", EditorStyles.boldLabel);
            newName = EditorGUILayout.TextField("Name", newName);
            newWidth = Mathf.Clamp(EditorGUILayout.IntField("Width (tiles)", newWidth), MinSize, MaxSize);
            newHeight = Mathf.Clamp(EditorGUILayout.IntField("Height (tiles)", newHeight), MinSize, MaxSize);
            if (GUILayout.Button("Create and add to catalog", GUILayout.Height(26))) SetLevel(CreateLevel(newName, newWidth, newHeight));
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Import a level code from the clipboard", GUILayout.Height(26))) ImportCode();
        }

        // ---- Level codes (shared with the in-game editor) -------------------------------------------------

        void CopyCode()
        {
            Save();
            GUIUtility.systemCopyBuffer = LevelCode.Encode(level.DisplayName, level.Par, level.Map);
            ShowNotification(new GUIContent("Level code copied"));
        }

        void ImportCode()
        {
            string text = GUIUtility.systemCopyBuffer;
            if (!LevelCode.TryDecode(text, out string name, out int par, out string map, out string error))
            {
                EditorUtility.DisplayDialog("Import level code",
                    string.IsNullOrWhiteSpace(text)
                        ? "The clipboard is empty. Copy a level code first (COPY CODE in the game's level editor)."
                        : "The clipboard does not hold a level code:\n\n" + error, "OK");
                return;
            }
            SetLevel(CreateLevel(name, map, par));
            ShowNotification(new GUIContent($"Imported \"{name}\""));
        }

        // ---- Grid ---------------------------------------------------------------------------------------------

        void DrawGridArea()
        {
            var area = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area, new Color(0.09f, 0.09f, 0.11f));
            var content = new Rect(0, 0, W * cell + 8, H * cell + 8);
            gridScroll = GUI.BeginScrollView(area, gridScroll, content);

            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(gridScroll.x / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(gridScroll.y / cell));
            int x1 = Mathf.Min(W, Mathf.CeilToInt((gridScroll.x + area.width) / cell));
            int y1 = Mathf.Min(H, Mathf.CeilToInt((gridScroll.y + area.height) / cell));

            if (e.type == EventType.Repaint)
            {
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        char c = grid[y][x];
                        var r = new Rect(x * cell, y * cell, cell - 1f, cell - 1f);
                        EditorGUI.DrawRect(r, LevelDataEditor.CellColor(c, x, y));
                        if (cell >= 16f) DrawGlyph(r, c);
                    }
                if (showPaths) DrawPaths(x0, y0, x1, y1);
                if (hover.x >= 0 && hover.x < W && hover.y >= 0 && hover.y < H)
                {
                    var hr = new Rect(hover.x * cell, hover.y * cell, cell - 1f, cell - 1f);
                    EditorGUI.DrawRect(hr, new Color(1f, 1f, 1f, 0.18f));
                }
            }

            HandleGridInput(id, e, area);
            GUI.EndScrollView();
        }

        void DrawGlyph(Rect r, char c)
        {
            string text = null;
            switch (c)
            {
                case 'S': text = "S"; break;
                case 'X': text = "X"; break;
                case 'o': case '~': case '1': case '2': case '3':
                case 'a': case 'b': case 'c': case 'A': case 'B': case 'C':
                    text = c.ToString();
                    break;
            }
            if (text == null) return;
            var old = GUI.contentColor;
            bool darkText = c == 'S' || c == 'X' || c == 'o' || c == 'a' || c == 'b' || c == 'c';   // drawn on bright fills
            GUI.contentColor = darkText ? Color.black : Color.white;
            GUI.Label(r, text, cellStyle);
            GUI.contentColor = old;
        }

        void HandleGridInput(int id, Event e, Rect area)
        {
            Vector2 mp = e.mousePosition;
            int cx = Mathf.FloorToInt(mp.x / cell), cy = Mathf.FloorToInt(mp.y / cell);
            bool inside = cx >= 0 && cy >= 0 && cx < W && cy < H;

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                var h = inside ? new Vector2Int(cx, cy) : new Vector2Int(-1, -1);
                if (h != hover) { hover = h; Repaint(); }
            }
            if (e.type == EventType.ScrollWheel && e.control && new Rect(gridScroll, area.size).Contains(e.mousePosition))
            {
                cell = Mathf.Clamp(cell - e.delta.y * 2f, 8f, 64f);
                e.Use();
                return;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (inside && (e.button == 0 || e.button == 1) && !e.alt)
                    {
                        GUIUtility.hotControl = id;
                        BeginStroke(new Vector2Int(cx, cy), e.button == 1);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        UpdateStroke(new Vector2Int(Mathf.Clamp(cx, 0, W - 1), Mathf.Clamp(cy, 0, H - 1)));
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        EndStroke();
                        e.Use();
                    }
                    break;
            }
        }

        // ---- Painting ---------------------------------------------------------------------------------------

        char ActiveGlyph => erasing ? '.' : glyph;

        void BeginStroke(Vector2Int p, bool erase)
        {
            erasing = erase;
            strokeStart = strokeLast = p;
            strokeChanged = false;
            strokeSnapshot = Snapshot();
            if (tool == Tool.Fill) { FloodFill(p, ActiveGlyph); return; }
            if (tool == Tool.Brush) SetCell(p.x, p.y, ActiveGlyph);
            else ApplyShape(p, p);
            Repaint();
        }

        void UpdateStroke(Vector2Int p)
        {
            switch (tool)
            {
                case Tool.Brush:
                    foreach (var c in Line(strokeLast, p)) SetCell(c.x, c.y, ActiveGlyph);
                    break;
                case Tool.Rectangle:
                case Tool.Box:
                    Restore(strokeSnapshot);
                    ApplyShape(strokeStart, p);
                    break;
            }
            strokeLast = p;
            Repaint();
        }

        void EndStroke()
        {
            strokeSnapshot = null;
            if (strokeChanged) Save();
        }

        void ApplyShape(Vector2Int a, Vector2Int b)
        {
            char g = ActiveGlyph;
            if (g == 'S' || g == 'X') { SetCell(b.x, b.y, g); return; }   // there is only ever one of each
            int xa = Mathf.Min(a.x, b.x), xb = Mathf.Max(a.x, b.x), ya = Mathf.Min(a.y, b.y), yb = Mathf.Max(a.y, b.y);
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    bool edge = x == xa || x == xb || y == ya || y == yb;
                    if (tool == Tool.Rectangle || edge) SetCell(x, y, g);
                }
        }

        void SetCell(int x, int y, char g)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            if (grid[y][x] == g) return;
            if (g == 'S' || g == 'X')
                for (int yy = 0; yy < H; yy++)
                    for (int xx = 0; xx < W; xx++)
                        if (grid[yy][xx] == g) grid[yy][xx] = '.';
            grid[y][x] = g;
            strokeChanged = true;
        }

        void FloodFill(Vector2Int start, char g)
        {
            char target = grid[start.y][start.x];
            if (target == g) return;
            if (g == 'S' || g == 'X') { SetCell(start.x, start.y, g); return; }
            var stack = new Stack<Vector2Int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.x < 0 || p.y < 0 || p.x >= W || p.y >= H || grid[p.y][p.x] != target) continue;
                grid[p.y][p.x] = g;
                strokeChanged = true;
                stack.Push(new Vector2Int(p.x + 1, p.y)); stack.Push(new Vector2Int(p.x - 1, p.y));
                stack.Push(new Vector2Int(p.x, p.y + 1)); stack.Push(new Vector2Int(p.x, p.y - 1));
            }
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

        char[][] Snapshot() => grid.Select(r => (char[])r.Clone()).ToArray();
        void Restore(char[][] snap) { for (int y = 0; y < H; y++) Array.Copy(snap[y], grid[y], W); }

        // ---- Resize -------------------------------------------------------------------------------------------

        void Resize(int left, int top, int right, int bottom)
        {
            int nw = W + left + right, nh = H + top + bottom;
            if (nw < MinSize || nh < MinSize || nw > MaxSize || nh > MaxSize) return;
            var n = new char[nh][];
            for (int y = 0; y < nh; y++)
            {
                n[y] = new string('.', nw).ToCharArray();
                int oy = y - top;
                if (oy < 0 || oy >= H) continue;
                for (int x = 0; x < nw; x++)
                {
                    int ox = x - left;
                    if (ox >= 0 && ox < W) n[y][x] = grid[oy][ox];
                }
            }
            grid = n;
            Save();
        }

        void WallBorder()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if ((x == 0 || y == 0 || x == W - 1 || y == H - 1) && grid[y][x] != 'S' && grid[y][x] != 'X') grid[y][x] = '#';
            Save();
        }

        // ---- Side panel -----------------------------------------------------------------------------------------

        void DrawSidePanel()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(so.FindProperty("par"));
            EditorGUILayout.PropertyField(so.FindProperty("hint"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Paint", EditorStyles.boldLabel);
            tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Brush", "Rectangle", "Box", "Fill" });
            DrawPalette();
            EditorGUILayout.LabelField($"Left click paints {Palette.First(p => p.Glyph == glyph).Name.ToLowerInvariant()}, right click erases.", EditorStyles.wordWrappedMiniLabel);
            cell = EditorGUILayout.Slider("Zoom", cell, 8f, 64f);
            showPaths = EditorGUILayout.ToggleLeft("Draw reference solution on the map", showPaths);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Size  {W} x {H}", EditorStyles.boldLabel);
            DrawResize();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            foreach (var c in checks) EditorGUILayout.HelpBox(c.text, c.type);

            EditorGUILayout.Space(6);
            DrawSolution();

            EditorGUILayout.Space(6);
            DrawCatalog();
        }

        void DrawPalette()
        {
            const int perRow = 6;
            float size = (SideWidth - 28f) / perRow;
            for (int i = 0; i < Palette.Length; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                    for (int j = i; j < Mathf.Min(i + perRow, Palette.Length); j++)
                    {
                        var entry = Palette[j];
                        var r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                        var swatch = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
                        EditorGUI.DrawRect(swatch, LevelDataEditor.CellColor(entry.Glyph, 0, 0));
                        DrawGlyph(swatch, entry.Glyph);
                        if (glyph == entry.Glyph) Outline(r, Color.white);
                        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                        GUI.Label(r, new GUIContent("", entry.Name));
                        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                        {
                            glyph = entry.Glyph;
                            Event.current.Use();
                            Repaint();
                        }
                    }
            }
        }

        static void Outline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), c);
        }

        void DrawResize()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Rows", GUILayout.Width(40));
                if (GUILayout.Button("+ top")) Resize(0, 1, 0, 0);
                if (GUILayout.Button("- top")) Resize(0, -1, 0, 0);
                if (GUILayout.Button("+ bottom")) Resize(0, 0, 0, 1);
                if (GUILayout.Button("- bottom")) Resize(0, 0, 0, -1);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Cols", GUILayout.Width(40));
                if (GUILayout.Button("+ left")) Resize(1, 0, 0, 0);
                if (GUILayout.Button("- left")) Resize(-1, 0, 0, 0);
                if (GUILayout.Button("+ right")) Resize(0, 0, 1, 0);
                if (GUILayout.Button("- right")) Resize(0, 0, -1, 0);
            }
            if (GUILayout.Button("Wall the border")) WallBorder();
            EditorGUILayout.LabelField("New tiles are floor. Hold Ctrl and scroll over the map to zoom.", EditorStyles.wordWrappedMiniLabel);
        }

        void DrawSolution()
        {
            EditorGUILayout.LabelField("Reference solution (one script per loop: U D L R move, . waits)", EditorStyles.boldLabel);
            var list = so.FindProperty("referenceSolution");
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{i + 1}", GUILayout.Width(16));
                    string clean = Regex.Replace((EditorGUILayout.TextField(item.stringValue) ?? "").ToUpperInvariant(), "[^UDLR.]", "");
                    if (clean != item.stringValue) { item.stringValue = clean; pathKey = null; }
                    if (GUILayout.Button("x", GUILayout.Width(20))) { list.DeleteArrayElementAtIndex(i); pathKey = null; break; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Loop")) { list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = ""; pathKey = null; }
                if (GUILayout.Button("Find final loop")) FindFinalLoop(list);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate"))
                {
                    so.ApplyModifiedProperties();
                    validationOk = LevelValidator.Validate(level, LevelValidator.FindLoopTicks(), true, out validation);
                }
                if (GUILayout.Button("Set par from solution") && list.arraySize > 0) so.FindProperty("par").intValue = list.arraySize;
            }
            if (!string.IsNullOrEmpty(solverNote)) EditorGUILayout.HelpBox(solverNote, MessageType.None);
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, validationOk ? MessageType.Info : MessageType.Error);
        }

        void FindFinalLoop(SerializedProperty list)
        {
            so.ApplyModifiedProperties();
            LevelLayout layout;
            try { layout = level.BuildLayout(); }
            catch (FormatException e) { solverNote = "Map error: " + e.Message; return; }

            int loopTicks = LevelValidator.FindLoopTicks();
            var earlier = new List<string>();
            for (int i = 0; i < list.arraySize - 1; i++) earlier.Add(list.GetArrayElementAtIndex(i).stringValue);
            var echoes = Solver.Record(layout, earlier, loopTicks);

            EditorUtility.DisplayProgressBar("Level Editor", $"Searching every possible loop {earlier.Count + 1} (up to 1,000,000 states)...", 0.5f);
            SearchResult result;
            try { result = Solver.FindLoop(layout, echoes, loopTicks, w => Solver.ReachedExit(layout, w) && Solver.AllEchoesAlive(w), 1_000_000); }
            finally { EditorUtility.ClearProgressBar(); }

            if (result.Script == null)
            {
                solverNote = result.Capped
                    ? $"Gave up after {result.StatesExplored:N0} states. The earlier loops may need more loops, or the level is too open to search."
                    : $"No loop {earlier.Count + 1} reaches the exit with every echo surviving ({result.StatesExplored:N0} states searched). The earlier loops need changing.";
                return;
            }
            if (list.arraySize == 0) list.arraySize = 1;
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = result.Script;
            solverNote = $"Fastest final loop found: {result.Script} ({result.Script.Length} moves, {result.StatesExplored:N0} states). Check the par.";
            pathKey = null;
        }

        void DrawCatalog()
        {
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            var catalog = FindCatalog();
            if (!catalog) { EditorGUILayout.HelpBox("No LevelCatalog asset found.", MessageType.Warning); return; }
            int index = catalog.IndexOf(level);
            if (index < 0)
            {
                EditorGUILayout.HelpBox("This level is not in the catalog, so the game cannot play it.", MessageType.Warning);
                if (GUILayout.Button("Add to catalog")) CatalogAdd(catalog, level);
                return;
            }
            EditorGUILayout.LabelField($"Tape {index + 1} of {catalog.Count}");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(index == 0)) if (GUILayout.Button("Move earlier")) CatalogMove(catalog, index, -1);
                using (new EditorGUI.DisabledScope(index == catalog.Count - 1)) if (GUILayout.Button("Move later")) CatalogMove(catalog, index, 1);
                if (GUILayout.Button("Remove")) CatalogRemove(catalog, index);
            }
        }

        // ---- Checks ----------------------------------------------------------------------------------------------

        void Analyze()
        {
            checks.Clear();
            LevelLayout layout;
            try { layout = LevelLayout.Parse(GridToMap()); }
            catch (FormatException e) { checks.Add((MessageType.Error, e.Message)); return; }

            checks.Add((MessageType.None, $"{layout.Plates.Count} plates, {layout.Doors.Count} doors, {layout.CrateStarts.Count} crates"));
            for (int ch = 1; ch <= LevelLayout.MaxChannels; ch++)
            {
                int plates = layout.PlateCount(ch), doors = layout.Doors.Count(d => d.Channel == ch);
                if (doors > 0 && plates == 0) checks.Add((MessageType.Warning, $"There are {ChannelNames[ch]} doors but no {ChannelNames[ch]} plate, so they never change."));
                if (plates > 0 && doors == 0) checks.Add((MessageType.Warning, $"There are {ChannelNames[ch]} plates but no {ChannelNames[ch]} door for them to control."));
            }
            if (!ExitReachable(layout)) checks.Add((MessageType.Warning, "The exit cannot be reached from the start even with every door open."));
            if (W * H > 700) checks.Add((MessageType.Info, "Large level: the board is scaled to fit the screen, and one loop is only " + LevelValidator.FindLoopTicks() + " moves long."));
        }

        static bool ExitReachable(LevelLayout layout)
        {
            var seen = new bool[layout.CellCount];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(layout.Start);
            seen[layout.Index(layout.Start)] = true;
            Vector2Int[] dirs = { Dir.Up, Dir.Down, Dir.Left, Dir.Right };
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p == layout.Exit) return true;
                foreach (var d in dirs)
                {
                    var n = p + d;
                    if (!layout.InBounds(n) || layout.IsWall(n)) continue;
                    int i = layout.Index(n);
                    if (seen[i] || layout.InitialGround(i) == GroundState.Hole) continue;
                    seen[i] = true;
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        // ---- Solution overlay -------------------------------------------------------------------------------------

        void EnsurePaths()
        {
            var list = so.FindProperty("referenceSolution");
            var loops = new List<string>();
            for (int i = 0; i < list.arraySize; i++) loops.Add(list.GetArrayElementAtIndex(i).stringValue);
            string key = lastMap + "#" + string.Join("|", loops);
            if (key == pathKey) return;
            pathKey = key;
            paths.Clear();
            paradoxCells.Clear();
            try
            {
                var layout = level.BuildLayout();
                var session = new LoopSession(layout, LevelValidator.FindLoopTicks());
                for (int k = 0; k < loops.Count; k++)
                {
                    session.Begin();
                    var path = new List<Vector2Int> { session.World.Player.Cell };
                    foreach (char c in loops[k])
                    {
                        if (session.Phase != LoopPhase.Playing || session.IsTimeUp) break;
                        session.Step(Dir.FromScriptChar(c));
                        path.Add(session.World.Player.Cell);
                    }
                    paths.Add(path);
                    foreach (var p in session.Paradoxes) paradoxCells.Add(p.Cell);
                    if (session.Phase == LoopPhase.Solved) break;
                    session.CommitLoop();
                    session.StartLoop();
                }
            }
            catch (Exception) { paths.Clear(); }
        }

        void DrawPaths(int x0, int y0, int x1, int y1)
        {
            EnsurePaths();
            boardPalette ??= AssetDatabase.FindAssets("t:BoardPalette").Select(g => AssetDatabase.LoadAssetAtPath<BoardPalette>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            float s = Mathf.Max(3f, cell * 0.26f);
            for (int k = 0; k < paths.Count; k++)
            {
                var color = boardPalette ? (k == paths.Count - 1 ? boardPalette.record : boardPalette.Echo(k)) : Color.HSVToRGB(k * 0.17f % 1f, 0.6f, 1f);
                color.a = 0.9f;
                var offset = new Vector2((k % 3 - 1) * s * 0.9f, (k / 3 % 3 - 1) * s * 0.9f);
                foreach (var p in paths[k])
                {
                    if (p.x < x0 || p.x >= x1 || p.y < y0 || p.y >= y1) continue;
                    var c = new Vector2((p.x + 0.5f) * cell, (p.y + 0.5f) * cell) + offset;
                    EditorGUI.DrawRect(new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s), color);
                }
            }
            var old = GUI.contentColor;
            GUI.contentColor = boardPalette ? boardPalette.paradox : Color.magenta;
            foreach (var p in paradoxCells)
                if (p.x >= x0 && p.x < x1 && p.y >= y0 && p.y < y1)
                    GUI.Label(new Rect(p.x * cell, p.y * cell, cell, cell), "X", cellStyle);
            GUI.contentColor = old;
        }

        // ---- Assets ------------------------------------------------------------------------------------------------

        static LevelCatalog FindCatalog()
        {
            var guid = AssetDatabase.FindAssets("t:LevelCatalog").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void CatalogAdd(LevelCatalog catalog, LevelData data)
        {
            var cso = new SerializedObject(catalog);
            var levels = cso.FindProperty("levels");
            levels.arraySize++;
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = data;
            cso.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static void CatalogMove(LevelCatalog catalog, int index, int delta)
        {
            var cso = new SerializedObject(catalog);
            cso.FindProperty("levels").MoveArrayElement(index, index + delta);
            cso.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static void CatalogRemove(LevelCatalog catalog, int index)
        {
            var cso = new SerializedObject(catalog);
            var levels = cso.FindProperty("levels");
            levels.GetArrayElementAtIndex(index).objectReferenceValue = null;   // clears the reference first, then drops the slot
            levels.DeleteArrayElementAtIndex(index);
            cso.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static string NextPath(string displayName)
        {
            Directory.CreateDirectory(LevelFolder);
            var catalog = FindCatalog();
            int number = (catalog ? catalog.Count : 0) + 1;
            string safe = Regex.Replace(displayName, "[^A-Za-z0-9]", "");
            if (safe.Length == 0) safe = "Level";
            return AssetDatabase.GenerateUniqueAssetPath($"{LevelFolder}/{number:00}_{safe}.asset");
        }

        static LevelData CreateLevel(string displayName, int width, int height)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < height; y++)
            {
                if (y > 0) sb.Append('\n');
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    sb.Append(border ? '#' : x == 1 && y == 1 ? 'S' : x == width - 2 && y == height - 2 ? 'X' : '.');
                }
            }
            return CreateLevel(displayName, sb.ToString(), 2);
        }

        /// <summary>Creates a level asset from a text map and adds it to the catalog.</summary>
        static LevelData CreateLevel(string displayName, string map, int par)
        {
            var data = CreateInstance<LevelData>();
            var so = new SerializedObject(data);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("par").intValue = Mathf.Max(1, par);
            so.FindProperty("map").stringValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
            string path = NextPath(displayName);
            AssetDatabase.CreateAsset(data, path);
            var catalog = FindCatalog();
            if (catalog) CatalogAdd(catalog, data);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<LevelData>(path);
        }

        void Duplicate()
        {
            string src = AssetDatabase.GetAssetPath(level);
            string dst = NextPath(level.DisplayName + " Copy");
            if (!AssetDatabase.CopyAsset(src, dst)) return;
            var copy = AssetDatabase.LoadAssetAtPath<LevelData>(dst);
            var cso = new SerializedObject(copy);
            cso.FindProperty("displayName").stringValue = level.DisplayName + " Copy";
            cso.ApplyModifiedPropertiesWithoutUndo();
            var catalog = FindCatalog();
            if (catalog) CatalogAdd(catalog, copy);
            AssetDatabase.SaveAssets();
            SetLevel(copy);
        }
    }

    /// <summary>Runs the Main scene in play mode and jumps straight to one level.</summary>
    [InitializeOnLoad]
    static class LevelPlaytest
    {
        const string MainScene = "Assets/Rewind/Scenes/Main.unity";
        const string Key = "Rewind.Playtest.Index";

        static LevelPlaytest() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Start(LevelData level)
        {
            var guid = AssetDatabase.FindAssets("t:LevelCatalog").FirstOrDefault();
            var catalog = guid == null ? null : AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            int index = catalog ? catalog.IndexOf(level) : -1;
            if (index < 0)
            {
                EditorUtility.DisplayDialog("Playtest", "Add this level to the catalog first; the game plays levels from the catalog.", "OK");
                return;
            }
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssets();
            if (EditorSceneManager.GetActiveScene().path != MainScene) EditorSceneManager.OpenScene(MainScene);
            SessionState.SetInt(Key, index);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(Key, -1) >= 0) EditorApplication.update += WaitForRouter;
            else if (state == PlayModeStateChange.ExitingPlayMode) { EditorApplication.update -= WaitForRouter; SessionState.EraseInt(Key); }
        }

        static void WaitForRouter()
        {
            var router = Object.FindFirstObjectByType<UIRouter>();
            if (!router || Time.frameCount < 3) return;
            EditorApplication.update -= WaitForRouter;
            int index = SessionState.GetInt(Key, -1);
            SessionState.EraseInt(Key);
            if (index >= 0) router.PlayLevel(index);
        }
    }
}
