using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Runs only the hose integration/rendering tests and writes a local result for file-based validation.</summary>
public static class HoseVfxVerification
{
    private const string RunningKey = "Fireline.HoseVfxTests.Running";
    private static HoseVfxTestResultWriter callback;

    [InitializeOnLoadMethod]
    private static void RestoreCallback()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (callback != null) TestRunnerApi.UnregisterTestCallback(callback);
        callback = new HoseVfxTestResultWriter();
        TestRunnerApi.RegisterTestCallback(callback);
    }

    [MenuItem("Fireline/VFX/Run Hose VFX Tests")]
    public static void Run()
    {
        var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        SessionState.SetBool(RunningKey, true);
        RestoreCallback();
        runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.PlayMode,
            testNames = new[] { "HoseVfxTests", "HoseVfxRenderingTests" }
        }));
    }
}

public sealed class HoseVfxTestResultWriter : ICallbacks
{
    public void RunStarted(ITestAdaptor testsToRun) { }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor result) { }

    public void RunFinished(ITestResultAdaptor result)
    {
        SessionState.SetBool("Fireline.HoseVfxTests.Running", false);
        TestRunnerApi.UnregisterTestCallback(this);
        Directory.CreateDirectory("Temp");
        TestRunnerApi.SaveResultToFile(result, "Temp/HoseVfxTests.xml");
        string summary = $"{result.ResultState}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.";
        File.WriteAllText("Temp/HoseVfxTests-result.txt", summary);
        Debug.Log("[Hose VFX tests] " + summary);
    }
}
