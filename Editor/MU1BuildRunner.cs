using System;
using System.IO;
using UnityEditor;

namespace BirthdayTactics.Editor
{
    [InitializeOnLoad]
    // Runs the milestone build after Unity reloads and finds the request marker.
    internal static class MU1BuildRunner
    {
        private const string RequestPath = "Temp/MU1BuildRequest.txt";
        private const string ResultPath = "TestResults/MU1-Build.txt";

        static MU1BuildRunner()
        {
            EditorApplication.delayCall += BuildIfRequested;
        }

        private static void BuildIfRequested()
        {
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ".");

            try
            {
                MU1Build.BuildWindows();
                File.WriteAllText(ResultPath, "status=Passed" + Environment.NewLine);
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath,
                    "status=Failed" + Environment.NewLine +
                    "message=" + exception + Environment.NewLine);
                throw;
            }
        }
    }
}
