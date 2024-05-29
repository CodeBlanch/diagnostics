// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions;
using TestRunner = Microsoft.Diagnostics.CommonTestRunner.TestRunner;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.UnitTests
{
    public class TracesPipelineUnitTests
    {
        private readonly ITestOutputHelper _output;

        public static IEnumerable<object[]> Configurations => TestRunner.Configurations;

        public TracesPipelineUnitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableTheory, MemberData(nameof(Configurations))]
        public async Task TestTracesPipeline(TestConfiguration config)
        {
            TestActivityLogger logger = new();

            await using (TestRunner testRunner = await PipelineTestUtilities.StartProcess(config, "TracesRemoteTest UseActivitySource", _output))
            {
                DiagnosticsClient client = new(testRunner.Pid);

                await using TracesPipeline pipeline = new(client, new TracesPipelineSettings
                {
                    ActivitySourceNames = new[] { "*" },
                    Duration = Timeout.InfiniteTimeSpan,
                }, new[] { logger });

                await PipelineTestUtilities.ExecutePipelineWithTracee(
                    pipeline,
                    testRunner);
            }

            Assert.Single(logger.LoggedActivities);

            var activity = logger.LoggedActivities[0];

            Assert.Equal("TestBodyCore", activity.OperationName);
            Assert.Equal("Display name", activity.DisplayName);
            Assert.True(activity.IsStopped);
            Assert.NotEqual(default, activity.TraceId);
            Assert.NotEqual(default, activity.SpanId);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.NotEqual(TimeSpan.Zero, activity.Duration);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal("Error occurred", activity.StatusDescription);

            Assert.NotNull(activity.Source);
            Assert.Equal("EventPipeTracee.ActivitySource", activity.Source.Name);

            Dictionary<string, object> tags = activity.TagObjects.ToDictionary(
                i => i.Key,
                i => i.Value);
            Assert.Equal("value1", tags["custom.tag.string"]);
            Assert.Equal("18", tags["custom.tag.int"]);
        }

        private sealed class TestActivityLogger : IActivityLogger
        {
            public List<Activity> LoggedActivities { get; } = new();

            public void Log(Activity activity)
            {
                LoggedActivities.Add(activity);
            }

            public Task PipelineStarted(CancellationToken token) => Task.CompletedTask;

            public Task PipelineStopped(CancellationToken token) => Task.CompletedTask;
        }
    }
}
