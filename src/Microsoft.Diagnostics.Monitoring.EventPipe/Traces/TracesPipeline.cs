// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal class TracesPipeline : EventSourcePipeline<TracesPipelineSettings>
    {
        private readonly IEnumerable<IActivityLogger> _loggers;

        public TracesPipeline(DiagnosticsClient client,
            TracesPipelineSettings settings,
            IEnumerable<IActivityLogger> loggers) : base(client, settings)
        {
            _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
        }

        protected override MonitoringSourceConfiguration CreateConfiguration()
            => new ActivitySourceConfiguration(Settings.Sources);

        protected override async Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            await ExecuteCounterLoggerActionAsync((logger) => logger.PipelineStarted(token)).ConfigureAwait(false);

            eventSource.Dynamic.All += traceEvent => {
                try
                {
                    if (traceEvent.TryGetActivityPayload(out ActivityData activityPayload))
                    {
                        ExecuteCounterLoggerAction((logger) => logger.Log(in activityPayload));
                    }
                }
                catch (Exception)
                {
                }
            };

            using EventTaskSource<Action> sourceCompletedTaskSource = new(
                taskComplete => taskComplete,
                handler => eventSource.Completed += handler,
                handler => eventSource.Completed -= handler,
                token);

            await sourceCompletedTaskSource.Task.ConfigureAwait(false);

            await ExecuteCounterLoggerActionAsync((logger) => logger.PipelineStopped(token)).ConfigureAwait(false);
        }

        private async Task ExecuteCounterLoggerActionAsync(Func<IActivityLogger, Task> action)
        {
            foreach (IActivityLogger logger in _loggers)
            {
                try
                {
                    await action(logger).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private void ExecuteCounterLoggerAction(Action<IActivityLogger> action)
        {
            foreach (IActivityLogger logger in _loggers)
            {
                try
                {
                    action(logger);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}
