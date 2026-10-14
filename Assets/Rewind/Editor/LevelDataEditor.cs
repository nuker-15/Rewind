using System;
using Rewind.Core;
using Rewind.Data;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    /// <summary>Shows a coloured preview of the map under the fields, and validates the level on request.</summary>
    [CustomEditor(typeof(LevelData))]
    public sealed class LevelDataEditor : Editor
    {
        string validation;
        bool validationOk;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var level = (LevelData)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            try
            {
                var layout = level.BuildLayout();
                DrawPreview(level.Map, layout);
                EditorGUILayout.LabelField($"{layout.Width} x {layout.Height}, {layout.Plates.Count} plates, {layout.Doors.Count} doors, {layout.CrateStarts.Count} crates");
            }
            catch (FormatException e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Error);
            }
            EditorGUILayout.HelpBox(LevelData.MapLegend, MessageType.None);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open in Level Editor")) LevelEditorWindow.OpenWith(level);
            if (GUILayout.Button("Validate level"))
            {
                validationOk = LevelValidator.Validate(level, LevelValidator.FindLoopTicks(level), true, out validation);
            }
            if (!string.IsNullOrEmpty(validation))
                EditorGUILayout.HelpBox(validation, validationOk ? MessageType.Info : MessageType.Error);
        }

        static void DrawPreview(string map, LevelLayout layout)
        {
            var rows = LevelLayout.SplitRows(map);
            float cell = Mathf.Clamp((EditorGUIUtility.currentViewWidth - 40f) / layout.Width, 6f, 22f);
            var rect = GUILayoutUtility.GetRect(layout.Width * cell, layout.Height * cell, GUILayout.ExpandWidth(false));
            for (int y = 0; y < rows.Count; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    var r = new Rect(rect.x + x * cell, rect.y + y * cell, cell - 1f, cell - 1f);
                    EditorGUI.DrawRect(r, CellColor(rows[y][x], x, y));
                }
        }

        static readonly Color Amber = new Color(1f, 0.71f, 0.28f), Cyan = new Color(0.28f, 0.85f, 1f), Orange = new Color(1f, 0.54f, 0.36f);

        static Color Channel(char c) => c switch { '2' or 'b' or 'B' => Cyan, '3' or 'c' or 'C' => Orange, _ => Amber };

        /// <summary>The preview colour of a map tile. Plates and doors use <paramref name="group"/> when given, else their colour number.</summary>
        internal static Color CellColor(char c, int x, int y, Color? group = null)
        {
            var g = group ?? Channel(c);
            switch (c)
            {
                case '#': return new Color(0.16f, 0.18f, 0.36f);
                case 'S': return Color.white;
                case 'X': return new Color(0.77f, 1f, 0.42f);
                case 'o': return new Color(0.79f, 0.55f, 0.31f);
                case '~': return new Color(0.49f, 0.51f, 0.7f);
                case '_': return new Color(0.02f, 0.02f, 0.05f);
                case '1': case '2': case '3': case 'p': return Color.Lerp(g, Color.black, 0.45f);
                case 'a': case 'b': case 'c': case 'd': return g;
                case 'A': case 'B': case 'C': case 'D': return (x + y) % 2 == 0 ? g : Color.Lerp(g, Color.black, 0.6f);
                case '+': return Cyan;
                default: return (x + y) % 2 == 0 ? new Color(0.07f, 0.08f, 0.17f) : new Color(0.08f, 0.09f, 0.19f);
            }
        }
    }
}
