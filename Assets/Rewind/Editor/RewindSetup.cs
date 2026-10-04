using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    /// <summary>
    /// One-time project setup. Generates the default sprites, sounds, data, input bindings, prefabs and the main
    /// scene as ordinary assets you can then edit. "Build Missing" never touches assets that already exist, so it
    /// is safe to run after customising; "Rebuild" regenerates everything in place (asset GUIDs are kept).
    /// </summary>
    public static partial class RewindSetup
    {
        const string Root = "Assets/Rewind";
        const string ArtDir = Root + "/Art/Sprites";
        const string AudioDir = Root + "/Audio";
        const string DataDir = Root + "/Data";
        const string LevelsDir = DataDir + "/Levels";
        const string InputDir = Root + "/Input";
        const string InputPath = InputDir + "/RewindControls.inputactions";
        const string PrefabDir = Root + "/Prefabs";
        const string GamePrefabDir = PrefabDir + "/Game";
        const string BoardDir = PrefabDir + "/Board";
        const string UIDir = PrefabDir + "/UI";
        const string UIWidgetDir = UIDir + "/Widgets";
        const string UIScreenDir = UIDir + "/Screens";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Main.unity";

        static bool overwrite;

        [MenuItem("Rewind/Setup/Build Missing Assets", priority = 0)]
        public static void BuildMissing() => Run(false);

        [MenuItem("Rewind/Setup/Rebuild All Generated Assets (overwrites)", priority = 1)]
        public static void RebuildAll()
        {
            if (Application.isBatchMode || EditorUtility.DisplayDialog("Rebuild Rewind assets",
                    "This regenerates every Rewind sprite, sound, data asset, prefab and the Main scene, replacing any edits you made to them.",
                    "Rebuild", "Cancel"))
                Run(true);
        }

        /// <summary>Entry point for automation (menu items, MCP, batch mode).</summary>
        public static void Run(bool overwriteExisting)
        {
            if (!EnsureTextMeshPro(() => Run(overwriteExisting))) return;
            overwrite = overwriteExisting;
            try
            {
                EnsureFolders();
                BuildArt();
                BuildAudio();
                BuildData();
                BuildBoardPrefabs();
                BuildGamePrefab();
                BuildUI();
                BuildScene();
                AssetDatabase.SaveAssets();
                Debug.Log("Rewind setup finished. Open Assets/Rewind/Scenes/Main.unity and press Play.");
            }
            finally
            {
                overwrite = false;
                EditorUtility.ClearProgressBar();
            }
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { ArtDir, AudioDir, LevelsDir, InputDir, GamePrefabDir, BoardDir, UIWidgetDir, UIScreenDir, SceneDir })
                EnsureFolder(dir);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static bool Exists(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
        static bool ShouldBuild(string path) => overwrite || !Exists(path);

        static void Progress(string what, float t) => EditorUtility.DisplayProgressBar("Rewind setup", what, t);

        // ---- Serialized field helpers ------------------------------------------------------------

        static SerializedProperty Prop(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            if (p == null) Debug.LogError($"Field '{field}' not found on {so.targetObject.GetType().Name}.");
            return p;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static T LoadPrefab<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go ? go.GetComponent<T>() : null;
        }
    }
}
