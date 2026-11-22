using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RTS.EditorTools
{
    /// <summary>
    /// One-time project configuration that cannot be expressed as committed asset files without an editor:
    /// installs the Universal Render Pipeline package, creates and assigns a URP asset, creates the Boot scene and
    /// registers it in Build Settings, and applies player settings. Runs automatically when the project is opened
    /// (once per editor session) and can be re-run from the "RTS/Setup" menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/_Project/Scenes/Boot/Boot.unity";
        const string UrpPackage = "com.unity.render-pipelines.universal";
        const string SettingsDir = "Assets/_Project/Settings";
        const string UrpAssetPath = SettingsDir + "/URP_PipelineAsset.asset";
        const string UrpRendererPath = SettingsDir + "/URP_Renderer.asset";
        const string SessionKey = "RTS_ProjectSetup_Ran";
        static AddRequest _add;

        static ProjectSetup()
        {
            // Run once the editor has finished importing and compiling (delayCall can fire while an import is still running).
            EditorApplication.update += RunWhenIdle;
        }

        static void RunWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= RunWhenIdle;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[RTS] Running automatic project setup (Boot scene, player settings, URP)...");
            try { RunAll(false); } catch (Exception ex) { Debug.LogWarning("[RTS] Automatic project setup failed: " + ex.Message + "\nUse RTS > Setup > Run Full Project Setup."); }
        }

        [MenuItem("RTS/Setup/Run Full Project Setup", priority = 0)]
        public static void RunAllMenu() => RunAll(true);

        public static void RunAll(bool verbose)
        {
            EnsureBootScene();
            ApplyPlayerSettings();
            if (UrpType("UniversalRenderPipelineAsset") == null) InstallUrp();
            else EnsureUrpAsset(verbose);
        }

        // ------------------------------------------------------------------ scenes
        [MenuItem("RTS/Setup/Create Boot Scene and Build Settings")]
        public static void EnsureBootScene()
        {
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var cam = new GameObject("Main Camera") { tag = "MainCamera" };
                var c = cam.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
                c.orthographic = true;
                SceneManager.MoveGameObjectToScene(cam, scene);
                var note = new GameObject("README - GameRoot spawns automatically at runtime");
                SceneManager.MoveGameObjectToScene(note, scene);
                EditorSceneManager.SaveScene(scene, BootScene);
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.Refresh();
                Debug.Log("[RTS] Created " + BootScene);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == BootScene))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(BootScene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[RTS] Registered Boot scene in Build Settings.");
            }
        }

        // ------------------------------------------------------------------ player settings
        [MenuItem("RTS/Setup/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "Aurite Front";
            PlayerSettings.companyName = "AuriteFrontTeam";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
        }

        // ------------------------------------------------------------------ URP
        static Type UrpType(string name)
        {
            // URP runtime types live in this assembly; resolving by assembly-qualified name avoids scanning the AppDomain.
            return Type.GetType("UnityEngine.Rendering.Universal." + name + ", Unity.RenderPipelines.Universal.Runtime", false);
        }

        [MenuItem("RTS/Setup/Install Universal Render Pipeline")]
        public static void InstallUrp()
        {
            if (_add != null) return;
            Debug.Log("[RTS] Installing " + UrpPackage + " via Package Manager (the editor will recompile when done)...");
            _add = UnityEditor.PackageManager.Client.Add(UrpPackage);
            EditorApplication.update += PollAdd;
        }

        static void PollAdd()
        {
            if (_add == null || !_add.IsCompleted) return;
            EditorApplication.update -= PollAdd;
            if (_add.Status == StatusCode.Success)
            {
                Debug.Log("[RTS] Installed " + _add.Result.packageId + ". The URP asset will be created after the script reload.");
                SessionState.SetBool(SessionKey, false);   // run again after domain reload
            }
            else Debug.LogWarning("[RTS] URP install failed: " + _add.Error?.message + "\nThe game still runs on the built-in pipeline; install 'Universal RP' from Window > Package Manager and run RTS > Setup again.");
            _add = null;
        }

        [MenuItem("RTS/Setup/Create and Assign URP Asset")]
        public static void EnsureUrpAssetMenu() => EnsureUrpAsset(true);

        public static void EnsureUrpAsset(bool verbose)
        {
            var assetType = UrpType("UniversalRenderPipelineAsset");
            var rendererType = UrpType("UniversalRendererData");
            if (assetType == null || rendererType == null) { if (verbose) Debug.LogWarning("[RTS] URP is not installed."); return; }
            var current = GraphicsSettings.defaultRenderPipeline;
            if (current != null && assetType.IsInstanceOfType(current)) { if (verbose) Debug.Log("[RTS] URP asset already assigned: " + AssetDatabase.GetAssetPath(current)); return; }

            Directory.CreateDirectory(SettingsDir);
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (pipeline == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpRendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(renderer, UrpRendererPath);
                }
                var create = assetType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 1);
                if (create == null) { Debug.LogWarning("[RTS] Could not find UniversalRenderPipelineAsset.Create; create a URP asset manually (Assets > Create > Rendering > URP Asset)."); return; }
                pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { renderer });
                AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
                AssetDatabase.SaveAssets();
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int levels = QualitySettings.names.Length;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(cur, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[RTS] Universal Render Pipeline asset created and assigned (" + UrpAssetPath + ").");
        }
    }
}
