using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Nofun
{
    public static class CommandLineBuild
    {
        public static void BuildAndroidApk()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();

                var outputPath = GetArg(args, "-customBuildPath") ?? GetArg(args, "-buildPath") ?? "Builds/Android/app.apk";
                outputPath = MakeProjectRelativePathAbsolute(outputPath);

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Ensure we produce an .apk (not .aab) and don’t export a Gradle project.
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                var scenes = GetScenes(args);

                if (scenes.Length == 0)
                {
                    throw new Exception(
                        "No scenes available to build. Add a scene in File → Build Settings, or pass -scenes 'Assets/Scenes/YourScene.unity'.");
                }

                foreach (var scene in scenes)
                {
                    var absoluteScenePath = MakeProjectRelativePathAbsolute(scene);
                    if (!File.Exists(absoluteScenePath))
                    {
                        throw new Exception($"Scene not found: {scene} (resolved to {absoluteScenePath})");
                    }
                }

                var buildOptions = BuildOptions.None;
                if (HasArg(args, "-development"))
                {
                    buildOptions |= BuildOptions.Development;
                }

                if (HasArg(args, "-allowDebugging"))
                {
                    buildOptions |= BuildOptions.AllowDebugging;
                }

                if (HasArg(args, "-connectProfiler"))
                {
                    buildOptions |= BuildOptions.ConnectWithProfiler;
                }

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = buildOptions,
                };

                Debug.Log($"Starting Android APK build → {outputPath}");
                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new Exception(
                        $"Build failed: {report.summary.result} (errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings})");
                }

                Debug.Log($"Build succeeded → {outputPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Command line build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static bool HasArg(string[] args, string name) => args.Any(a => a == name);

        private static string[] GetScenes(string[] args)
        {
            var explicitScenes = GetArg(args, "-scenes");
            if (!string.IsNullOrWhiteSpace(explicitScenes))
            {
                return explicitScenes
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(MakeScenePathProjectRelative)
                    .ToArray();
            }

            var buildSettingsEnabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (buildSettingsEnabledScenes.Length > 0)
            {
                return buildSettingsEnabledScenes;
            }

            // Common default for this project (avoids having to open Build Settings).
            const string defaultScene = "Assets/Scenes/EmulatorScene.unity";
            if (File.Exists(MakeProjectRelativePathAbsolute(defaultScene)))
            {
                return new[] { defaultScene };
            }

            // Fallback: include any scene in the project.
            return AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }

        private static string MakeScenePathProjectRelative(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return scenePath;
            }

            if (!Path.IsPathRooted(scenePath))
            {
                return scenePath;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(scenePath);

            if (fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return fullPath.Substring(projectRoot.Length + 1).Replace('\\', '/');
            }

            return scenePath;
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string MakeProjectRelativePathAbsolute(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }
    }
}
