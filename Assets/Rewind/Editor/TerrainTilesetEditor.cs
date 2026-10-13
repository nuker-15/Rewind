using System.IO;
using System.Linq;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    /// <summary>Shows every wall and hole tile as it will be drawn, and can save the generated ones as PNGs to hand-edit.</summary>
    [CustomEditor(typeof(TerrainTileset))]
    public sealed class TerrainTilesetEditor : Editor
    {
        const int Columns = 8;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Blob auto-tiles: a wall (or a hole) picks its tile from which of its eight neighbours are the same kind. " +
                "Neighbour bits: N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128; a corner only counts when both edges beside it do, " +
                "which leaves 47 tiles per set. The previews below show each slot as \"slot / mask\". Put a sprite in a slot to " +
                "override that tile; empty slots are generated from the settings.", MessageType.None);
            DrawDefaultInspector();

            var tileset = (TerrainTileset)target;
            var palette = FindPalette();
            DrawSet("Wall tiles", TerrainTileset.Kind.Wall, tileset, palette);
            DrawSet("Hole tiles", TerrainTileset.Kind.Hole, tileset, palette);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Save generated tiles as PNGs and use them", GUILayout.Height(26))) Export(tileset, palette);
            if (GUILayout.Button("Clear tile overrides (back to generated)")) ClearOverrides();
        }

        static BoardPalette FindPalette()
        {
            var guid = AssetDatabase.FindAssets("t:BoardPalette").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<BoardPalette>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void DrawSet(string title, TerrainTileset.Kind kind, TerrainTileset tileset, BoardPalette palette)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            float size = Mathf.Clamp((EditorGUIUtility.currentViewWidth - 40f) / Columns, 28f, 72f);
            var floor = palette ? palette.floorA : new Color(0.07f, 0.08f, 0.17f);
            int rows = Mathf.CeilToInt(TerrainTileset.TileCount / (float)Columns);
            for (int row = 0; row < rows; row++)
            {
                var line = GUILayoutUtility.GetRect(size * Columns, size, GUILayout.ExpandWidth(false));
                for (int col = 0; col < Columns; col++)
                {
                    int slot = row * Columns + col;
                    if (slot >= TerrainTileset.TileCount) break;
                    var r = new Rect(line.x + col * size, line.y, size - 3f, size - 3f);
                    EditorGUI.DrawRect(r, floor);
                    var sprite = tileset.Tile(kind, TerrainTileset.Masks[slot], palette);
                    if (sprite)
                    {
                        var tex = sprite.texture;
                        var tr = sprite.textureRect;
                        GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                    }
                    GUI.Label(r, $"{slot}/{TerrainTileset.Masks[slot]}", EditorStyles.miniLabel);
                }
            }
        }

        void Export(TerrainTileset tileset, BoardPalette palette)
        {
            string root = $"{Path.GetDirectoryName(AssetDatabase.GetAssetPath(tileset))}/{tileset.name}_Tiles".Replace('\\', '/');
            var walls = ExportSet(tileset, TerrainTileset.Kind.Wall, palette, $"{root}/Walls");
            var holes = ExportSet(tileset, TerrainTileset.Kind.Hole, palette, $"{root}/Holes");
            AssetDatabase.Refresh();
            foreach (var path in walls.Concat(holes)) ImportAsSprite(path, tileset.PixelsPerTile);

            serializedObject.Update();
            Assign(serializedObject.FindProperty("wallTiles"), walls);
            Assign(serializedObject.FindProperty("holeTiles"), holes);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Terrain tiles",
                $"Saved {walls.Length} wall and {holes.Length} hole tiles to {root} and set them as this tileset's tiles.\n\n" +
                "Each file is named after its slot and neighbour mask. Edit them in any image editor and Unity picks the changes up. " +
                "The generated settings no longer affect these slots; use \"Clear tile overrides\" to go back to generated tiles.", "OK");
        }

        static string[] ExportSet(TerrainTileset tileset, TerrainTileset.Kind kind, BoardPalette palette, string folder)
        {
            Directory.CreateDirectory(folder);
            var paths = new string[TerrainTileset.TileCount];
            for (int slot = 0; slot < TerrainTileset.TileCount; slot++)
            {
                var tex = tileset.BuildTexture(kind, slot, palette, true);
                paths[slot] = $"{folder}/{kind}_{slot:00}_m{TerrainTileset.Masks[slot]:000}.png";
                File.WriteAllBytes(paths[slot], tex.EncodeToPNG());
                DestroyImmediate(tex);
            }
            return paths;
        }

        static void ImportAsSprite(string path, int pixelsPerUnit)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void Assign(SerializedProperty slots, string[] paths)
        {
            slots.arraySize = TerrainTileset.TileCount;
            for (int i = 0; i < paths.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
        }

        void ClearOverrides()
        {
            serializedObject.Update();
            foreach (var name in new[] { "wallTiles", "holeTiles" })
            {
                var slots = serializedObject.FindProperty(name);
                for (int i = 0; i < slots.arraySize; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
