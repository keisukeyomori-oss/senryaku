using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BirthdayTactics.Editor
{
    public static class MU0Build
    {
        public static void BuildWindows()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            const string executablePath = "Builds/Windows/BirthdayTactics.exe";

            Directory.CreateDirectory(Path.GetDirectoryName(executablePath) ?? "Builds/Windows");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"M-U0 Windows build failed: {report.summary.result}");
            }

            Debug.Log($"M-U0 Windows build succeeded: {report.summary.outputPath}");
        }
    }
}
