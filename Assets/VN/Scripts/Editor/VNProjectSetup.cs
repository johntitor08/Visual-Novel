// VNProjectSetup.cs -- first-run wiring: TMP resources, the play scene, and build settings.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VN;

namespace VNEditor
{
    [InitializeOnLoad]
    public static class VNProjectSetup
    {
        public const string ScenePath = "Assets/VN/Scenes/VisualNovel.unity";
        const string TmpImportGuard = "VN.TmpImportAttempted";

        static VNProjectSetup()
        {
            EditorApplication.delayCall += RunOnce;
        }

        static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EnsureTmpResources();
            if (!File.Exists(ScenePath)) CreateScene(false);
            EnsureInBuildSettings();
        }

        // ---------------------------------------------------------------- TextMeshPro

        /// <summary>
        /// TMP ships its default font and shaders in a unitypackage that has to be imported once.
        /// Without it every TextMeshProUGUI in the game renders nothing, so do it unattended.
        /// </summary>
        static void EnsureTmpResources()
        {
            if (TMP_Settings.instance != null) return;
            if (SessionState.GetBool(TmpImportGuard, false)) return;   // one attempt per session
            SessionState.SetBool(TmpImportGuard, true);

            Debug.Log("[VN] Importing TMP Essential Resources (needed for all in-game text)...");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("Visual Novel/Import TMP Essentials", false, 40)]
        static void ForceTmpImport()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        // ---------------------------------------------------------------- scene

        [MenuItem("Visual Novel/Create Play Scene", false, 20)]
        static void CreateSceneMenu() { CreateScene(true); }

        static void CreateScene(bool openAfter)
        {
            Directory.CreateDirectory("Assets/VN/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(camGo, scene);

            var gameGo = new GameObject("VisualNovel", typeof(VNGame));
            SceneManager.MoveGameObjectToScene(gameGo, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[VN] Created " + ScenePath + " -- open it and press Play.");

            if (openAfter) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else EditorSceneManager.CloseScene(scene, true);

            AssetDatabase.Refresh();
        }

        static void EnsureInBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath) continue;
                if (i == 0 && scenes[i].enabled) return;
                scenes.RemoveAt(i);
                break;
            }
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- convenience

        [MenuItem("Visual Novel/Play", false, 0)]
        static void Play()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }

            if (!File.Exists(ScenePath)) CreateScene(false);
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Visual Novel/Open Save Folder", false, 60)]
        static void OpenSaveFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [MenuItem("Visual Novel/Delete All Saves", false, 61)]
        static void DeleteSaves()
        {
            if (!EditorUtility.DisplayDialog("Delete all saves?",
                    "This removes every save slot and the ending record.", "Delete", "Cancel")) return;

            for (int i = 0; i < VNSaveSystem.SlotCount; i++) VNSaveSystem.Delete(i);
            VNSaveSystem.WriteGlobal(new VNGlobalData());
            Debug.Log("[VN] Saves cleared.");
        }
    }
}
