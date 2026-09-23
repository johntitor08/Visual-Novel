// VNBuild.cs -- one-click / headless builds from the project's build profiles.
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
        const string ProfileDir = "Assets/Settings/Build Profiles/";

        const string WebProfile = ProfileDir + "Web.asset";
        const string WebOutput = "Builds/Web";

        const string LinuxProfile = ProfileDir + "Linux.asset";
        const string LinuxOutput = "Builds/Linux";
        const string LinuxExecutable = "WhereTheSignalEnds.x86_64";

        [MenuItem("Visual Novel/Build Web", priority = 40)]
        public static void BuildWebMenu()
        {
            if (Build(WebProfile, WebOutput, WebOutput))
                EditorUtility.RevealInFinder(Path.GetFullPath(WebOutput));
        }

        [MenuItem("Visual Novel/Build Linux", priority = 41)]
        public static void BuildLinuxMenu()
        {
            if (Build(LinuxProfile, LinuxOutput, Path.Combine(LinuxOutput, LinuxExecutable)))
                EditorUtility.RevealInFinder(Path.GetFullPath(LinuxOutput));
        }

        /// <summary>
        /// Headless entry points, e.g.
        ///   Unity -batchmode -buildTarget WebGL   -projectPath ... -executeMethod VNEditor.VNBuild.BuildWebBatch
        ///   Unity -batchmode -buildTarget Linux64 -projectPath ... -executeMethod VNEditor.VNBuild.BuildLinuxBatch
        /// Both exit with 0 on success so a script can tell the outcomes apart.
        /// </summary>
        public static void BuildWebBatch()
        {
            Exit(() => Build(WebProfile, WebOutput, WebOutput));
        }

        public static void BuildLinuxBatch()
        {
            Exit(() => Build(LinuxProfile, LinuxOutput, Path.Combine(LinuxOutput, LinuxExecutable)));
        }

        static void Exit(Func<bool> build)
        {
            bool ok = false;
            try { ok = build(); }
            catch (Exception e) { Debug.LogError("[VN] Build threw: " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <param name="outputDir">Folder that receives the player.</param>
        /// <param name="location">What BuildPlayer is pointed at: the folder for Web, the executable for standalone.</param>
        static bool Build(string profilePath, string outputDir, string location)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
            {
                Debug.LogError("[VN] Build profile not found at " + profilePath);
                return false;
            }

            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = location,
                options = BuildOptions.None
            };

            var started = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log(string.Format(
                "[VN] {0} build {1}: {2} error(s), {3} warning(s), {4:0.0} MB, {5:0}s -> {6}",
                Path.GetFileNameWithoutExtension(profilePath), summary.result, summary.totalErrors,
                summary.totalWarnings, summary.totalSize / (1024.0 * 1024.0),
                (DateTime.Now - started).TotalSeconds, Path.GetFullPath(outputDir)));

            return summary.result == BuildResult.Succeeded;
        }
    }
}
