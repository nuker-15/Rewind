using System.Linq;
using Rewind.Gameplay;
using Rewind.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Rewind.EditorTools
{
    // The Main scene: camera, Game, Board and UIRoot prefab instances, and an EventSystem. Nothing is created at runtime.
    public static partial class RewindSetup
    {
        static void BuildScene()
        {
            Progress("Building the Main scene", 0.95f);
            if (!ShouldBuild(ScenePath))
            {
                AddSceneToBuild();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("Rewind setup skipped the scene because the current scene has unsaved changes.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = Assets.Palette;

            var game = ((GameObject)PrefabUtility.InstantiatePrefab(Prefabs.Game, scene)).GetComponent<RewindGame>();
            var board = ((GameObject)PrefabUtility.InstantiatePrefab(Prefabs.Board, scene)).GetComponent<BoardView>();
            Set(board, "game", game);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = palette.background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            var framer = camGo.AddComponent<BoardCameraFramer>();
            Set(framer, "board", board);
            Set(framer, "palette", palette);

            var ui = ((GameObject)PrefabUtility.InstantiatePrefab(Screens.Root, scene)).GetComponent<UIRouter>();
            Set(ui, "game", game);

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
        }

        static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            foreach (var s in scenes) s.enabled = false;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- TextMeshPro essentials -----------------------------------------------------------------------

        /// <summary>Imports TMP Essential Resources if needed. Returns false if an import was started.</summary>
        static bool EnsureTextMeshPro(System.Action resume)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            Debug.Log("Rewind setup: importing TextMeshPro Essential Resources first; setup continues when the import finishes.");
            void Done(string packageName)
            {
                AssetDatabase.importPackageCompleted -= Done;
                AssetDatabase.importPackageFailed -= Failed;
                EditorApplication.delayCall += () => resume();
            }
            void Failed(string packageName, string error)
            {
                AssetDatabase.importPackageCompleted -= Done;
                AssetDatabase.importPackageFailed -= Failed;
                Debug.LogError($"Could not import TextMeshPro essentials ({error}). Use Window > TextMeshPro > Import TMP Essential Resources, then run Rewind > Setup again.");
            }
            AssetDatabase.importPackageCompleted += Done;
            AssetDatabase.importPackageFailed += Failed;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            return false;
        }
    }
}
