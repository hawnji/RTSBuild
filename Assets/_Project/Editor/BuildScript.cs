using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RTS.EditorTools
{
    /// <summary>
    /// Windows build entry points.
    /// Menu: RTS > Build > Windows (64-bit).
    /// Command line:
    ///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod RTS.EditorTools.BuildScript.BuildWindows -logFile build.log
    /// Output: Builds/Windows/AuriteFront.exe (override with -buildPath &lt;path&gt;).
    /// </summary>
    public static class BuildScript
    {
        const string DefaultPath = "Builds/Windows/AuriteFront.exe";

        [MenuItem("RTS/Build/Windows (64-bit)", priority = 10)]
        public static void BuildWindowsMenu() => Build(DefaultPath, false);

        [MenuItem("RTS/Build/Windows (64-bit, development)", priority = 11)]
        public static void BuildWindowsDevMenu() => Build("Builds/WindowsDev/AuriteFront.exe", true);

        /// <summary>Command-line entry point.</summary>
        public static void BuildWindows()
        {
            string path = DefaultPath;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildPath") path = args[i + 1];
            bool ok = Build(path, args.Contains("-development"));
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(string path, bool development)
        {
            ProjectSetup.EnsureBootScene();
            ProjectSetup.ApplyPlayerSettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[RTS] No scenes to build."); return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[RTS] Build succeeded: {Path.GetFullPath(path)} ({s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:0}s)");
                return true;
            }
            Debug.LogError($"[RTS] Build {s.result}: {s.totalErrors} errors. See the editor log for details.");
            return false;
        }
    }
}
