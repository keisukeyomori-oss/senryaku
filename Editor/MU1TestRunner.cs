using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace BirthdayTactics.Editor
{
    [InitializeOnLoad]
    internal static class MU1TestRunner
    {
        private const string RequestPath = "Temp/MU1TestRequest.txt";
        private const string ResultPath = "TestResults/MU1-EditMode.txt";

        static MU1TestRunner()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        private static void RunIfRequested()
        {
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "BirthdayTactics.Core.Tests" }
            }));
        }

        private sealed class ResultWriter : ICallbacks
        {
            private readonly TestRunnerApi _api;

            public ResultWriter(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ".");
                File.WriteAllText(ResultPath,
                    $"status={result.TestStatus}{Environment.NewLine}" +
                    $"passed={result.PassCount}{Environment.NewLine}" +
                    $"failed={result.FailCount}{Environment.NewLine}" +
                    $"skipped={result.SkipCount}{Environment.NewLine}" +
                    $"inconclusive={result.InconclusiveCount}{Environment.NewLine}" +
                    $"message={result.Message}{Environment.NewLine}");
                Debug.Log($"M-U1 EditMode tests: {result.PassCount} passed, {result.FailCount} failed.");
                ScriptableObject.DestroyImmediate(_api);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
