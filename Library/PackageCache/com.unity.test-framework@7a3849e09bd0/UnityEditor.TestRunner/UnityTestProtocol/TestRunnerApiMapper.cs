using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.TestTools.TestRunner.GUI;

namespace UnityEditor.TestTools.TestRunner.UnityTestProtocol
{
    internal class TestRunnerApiMapper : ITestRunnerApiMapper
    {
        internal IGuiHelper guiHelper =  new GuiHelper(new MonoCecilHelper(), new AssetsDatabaseHelper());
        private readonly string _projectRepoPath;
        private readonly Dictionary<string, int> _iterationCounters = new Dictionary<string, int>();

        // Bridges spanId between TestStarted and TestFinished. Required because NUnit can re-run the
        // same test name multiple times (e.g. parameterized fixtures) without incrementing RetryIteration
        // (RetryCommand only retries on ResultState.Failure — skipped/inconclusive tests break immediately).
        // _iterationCounters increments on every TestStarted call, but TestResultAdaptor.RetryIteration
        // stays 0 for non-failure re-runs, so independently computing the spanId on both sides would diverge.
        // On domain reload this dictionary is lost; the fallback recomputes from RetryIteration, which is
        // correct for executing tests (the only kind that can trigger a domain reload).
        private readonly Dictionary<string, string> _activeSpanIds = new Dictionary<string, string>();

        // Per-process salt ensures spanIds are unique across editor runs within the same UTR session.
        // Without this, tests with identical FullNames in different packages (e.g. PackageIsolationTests)
        // produce colliding spanIds, corrupting the consumer's message tree.
        // Uses process ID because it survives domain reloads (same process) but differs across
        // editor launches (different processes).
        private readonly string _runSalt = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();

        public TestRunnerApiMapper(string projectRepoPath)
        {
            _projectRepoPath = projectRepoPath;
        }

        public TestPlanMessage MapTestToTestPlanMessage(ITestAdaptor testsToRun)
        {
            _iterationCounters.Clear();
            _activeSpanIds.Clear();
            var testsNames = testsToRun != null ? FlattenTestNames(testsToRun) : new List<string>();

            var msg = new TestPlanMessage
            {
                tests = testsNames
            };

            return msg;
        }

        public TestStartedMessage MapTestToTestStartedMessage(ITestAdaptor test)
        {
            var spanKey = test.UniqueName ?? test.FullName;
            _iterationCounters.TryGetValue(spanKey, out var iteration);
            _iterationCounters[spanKey] = iteration + 1;

            var spanId = DeterministicGuid.Create(spanKey, iteration, _runSalt);
            _activeSpanIds[spanKey] = spanId;

            return new TestStartedMessage
            {
                name = test.FullName,
                spanId = spanId
            };
        }

        public TestFinishedMessage TestResultToTestFinishedMessage(ITestResultAdaptor result)
        {
            string filePathString = default;
            int lineNumber = default;
            if (result.Test.Method != null && result.Test.TypeInfo != null)
            {
                var method = result.Test.Method.MethodInfo;
                var type = result.Test.TypeInfo.Type;
                var fileOpenInfo = guiHelper.GetFileOpenInfo(type, method);
                filePathString = !string.IsNullOrEmpty(_projectRepoPath) ? Path.Combine(_projectRepoPath, fileOpenInfo.FilePath) : fileOpenInfo.FilePath;
                lineNumber = fileOpenInfo.LineNumber;
            }

            var iteration = 0;
            if(result is TestResultAdaptor)
            {
                var adaptor = ((TestResultAdaptor)result);
                iteration = adaptor.RepeatIteration == 0 ? adaptor.RetryIteration : adaptor.RepeatIteration;
            }

            var spanKey = result.Test.UniqueName ?? result.Test.FullName;
            string spanId;
            if (_activeSpanIds.TryGetValue(spanKey, out spanId))
                _activeSpanIds.Remove(spanKey);
            else
                spanId = DeterministicGuid.Create(spanKey, iteration, _runSalt);

            return new TestFinishedMessage
            {
                name = result.Test.FullName,
                duration = Convert.ToUInt64(result.Duration * 1000),
                durationMicroseconds = Convert.ToUInt64(result.Duration * 1000000),
                message = result.Message,
                state = GetTestStateFromResult(result),
                stackTrace = result.StackTrace,
                fileName = filePathString,
                lineNumber = lineNumber,
                iteration = iteration,
                spanId = spanId
            };
        }

        public string GetRunStateFromResultNunitXml(ITestResultAdaptor result)
        {
            var doc = new XmlDocument();
            doc.LoadXml(result.ToXml().OuterXml);
            return doc.FirstChild.Attributes["runstate"].Value;
        }

        public TestState GetTestStateFromResult(ITestResultAdaptor result)
        {
            var state = TestState.Failure;

            if (result.TestStatus == TestStatus.Passed)
            {
                state = TestState.Success;
            }
            else if (result.TestStatus == TestStatus.Skipped)
            {
                state = TestState.Skipped;

                if (result.ResultState.ToLowerInvariant().EndsWith("ignored"))
                {
                    state = TestState.Ignored;
                }
            }
            else
            {
                if (result.ResultState.ToLowerInvariant().Equals("inconclusive"))
                {
                    state = TestState.Inconclusive;
                }

                if (result.ResultState.ToLowerInvariant().EndsWith("cancelled") ||
                    result.ResultState.ToLowerInvariant().EndsWith("error"))
                {
                    state = TestState.Error;
                }
            }

            return state;
        }

        public List<string> FlattenTestNames(ITestAdaptor test)
        {
            var results = new List<string>();

            if (!test.IsSuite)
                results.Add(test.FullName);

            if (test.Children != null && test.Children.Any())
                foreach (var child in test.Children)
                    results.AddRange(FlattenTestNames(child));

            return results;
        }
    }
}
