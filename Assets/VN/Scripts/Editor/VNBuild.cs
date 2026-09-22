// VNBuild.cs -- one-click / headless Web build using the project's Web build profile.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VNEditor
{
    public static class VNBuild
    {
        const string ProfilePath = "Assets/Settings/Build Profiles/Web.asset";
        const string OutputDir = "Builds/Web";

        [MenuItem("Visual Novel/Build Web", priority = 40)]
        public static void BuildWebMenu()
        {
            if (BuildWeb())
                EditorUtility.RevealInFinder(Path.GetFullPath(OutputDir));
        }

        /// <summary>
        /// Entry point for:
        ///   Unity -batchmode -buildTarget WebGL -projectPath ... -executeMethod VNEditor.VNBuild.BuildWebBatch
        /// Exits with 0 on success so a script can tell the two apart.
        /// </summary>
        public static void BuildWebBatch()
        {
            bool ok = false;
            try { ok = BuildWeb(); }
            catch (Exception e) { Debug.LogError("[VN] Web build threw: " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool BuildWeb()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("[VN] Build profile not found at " + ProfilePath);
                return false;
            }

            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = OutputDir,
                options = BuildOptions.None
            };

            var started = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log(string.Format(
                "[VN] Web build {0}: {1} error(s), {2} warning(s), {3:0.0} MB, {4:0}s -> {5}",
                summary.result, summary.totalErrors, summary.totalWarnings,
                summary.totalSize / (1024.0 * 1024.0), (DateTime.Now - started).TotalSeconds,
                Path.GetFullPath(OutputDir)));

            return summary.result == BuildResult.Succeeded;
        }
    }
}
