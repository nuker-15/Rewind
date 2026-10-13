using System.IO;
using System.Linq;
using Rewind.Data;
using Rewind.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    /// <summary>Shows the 16 wall tiles as they will be drawn, and can save the generated ones as PNGs to hand-edit.</summary>
    [CustomEditor(typeof(TerrainTileset))]
    public sealed class TerrainTilesetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Dual-grid walls: each tile sits on a corner of the cell grid and is chosen by which of the four cells around that corner " +
                "are wall (top-left 8, top-right 4, bottom-left 2, bottom-right 1). Put a sprite in a slot to override that tile; " +
                "empty slots are generated from the settings below.", MessageType.None);
            DrawDefaultInspector();

            var tileset = (TerrainTileset)target;
            var palette = FindPalette();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tiles, by index", EditorStyles.boldLabel);
            DrawTiles(tileset, palette);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Save generated tiles as PNGs and use them", GUILayout.Height(26))) Export(tileset, palette);
            if (GUILayout.Button("Clear tile overrides (back to generated)")) ClearOverrides();
        }

        static BoardPalette FindPalette()
        {
            var guid = AssetDatabase.FindAssets("t:BoardPalette").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<BoardPalette>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void DrawTiles(TerrainTileset tileset, BoardPalette palette)
        {
            float size = Mathf.Clamp((EditorGUIUtility.currentViewWidth - 40f) / 4f, 32f, 80f);
            var floor = palette ? palette.floorA : new Color(0.07f, 0.08f, 0.17f);
            for (int row = 0; row < 4; row++)
            {
                var line = GUILayoutUtility.GetRect(size * 4f, size, GUILayout.ExpandWidth(false));
                for (int col = 0; col < 4; col++)
                {
                    int mask = row * 4 + col;
                    var r = new Rect(line.x + col * size, line.y, size - 4f, size - 4f);
                    EditorGUI.DrawRect(r, floor);
                    var sprite = tileset.WallTile(mask, palette);
                    if (sprite)
                    {
                        var tex = sprite.texture;
                        var tr = sprite.textureRect;
                        GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                    }
                    GUI.Label(r, mask.ToString(), EditorStyles.miniLabel);
                }
            }
        }

        void Export(TerrainTileset tileset, BoardPalette palette)
        {
            string assetPath = AssetDatabase.GetAssetPath(tileset);
            string folder = $"{Path.GetDirectoryName(assetPath)}/{tileset.name}_Tiles".Replace('\\', '/');
            Directory.CreateDirectory(folder);

            var paths = new string[TerrainTileset.TileCount];
            for (int mask = 1; mask < TerrainTileset.TileCount; mask++)
            {
                var tex = tileset.BuildTexture(mask, palette, true);
                paths[mask] = $"{folder}/Wall_{mask:00}.png";
                File.WriteAllBytes(paths[mask], tex.EncodeToPNG());
                DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();

            for (int mask = 1; mask < TerrainTileset.TileCount; mask++)
            {
                if (!(AssetImporter.GetAtPath(paths[mask]) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = tileset.PixelsPerTile;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            serializedObject.Update();
            var slots = serializedObject.FindProperty("wallTiles");
            slots.arraySize = TerrainTileset.TileCount;
            for (int mask = 1; mask < TerrainTileset.TileCount; mask++)
                slots.GetArrayElementAtIndex(mask).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[mask]);
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Terrain tiles",
                $"Saved 15 tiles to {folder} and set them as this tileset's wall tiles.\n\n" +
                "Edit the PNGs in any image editor and Unity picks the changes up. The generated settings no longer affect " +
                "these slots; use \"Clear tile overrides\" to go back to generated tiles.", "OK");
        }

        void ClearOverrides()
        {
            serializedObject.Update();
            var slots = serializedObject.FindProperty("wallTiles");
            for (int i = 0; i < slots.arraySize; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
