// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.NETCore.Client;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    public sealed class ActivitySourceConfiguration : MonitoringSourceConfiguration
    {
        private readonly string[] _ActivitySourceNames;

        public ActivitySourceConfiguration(IEnumerable<string>? activitySourceNames)
        {
            _ActivitySourceNames = activitySourceNames?.ToArray() ?? Array.Empty<string>();
        }

        public override IList<EventPipeProvider> GetProviders()
        {
            StringBuilder filterAndPayloadSpecs = new();
            foreach (string activitySource in _ActivitySourceNames)
            {
                if (string.IsNullOrEmpty(activitySource))
                {
                    continue;
                }

                // Note: It isn't currently possible to get Events or Links off
                // of Activity using this mechanism:
                // Events=Events.*Enumerate;Links=Links.*Enumerate; See:
                // https://github.com/dotnet/runtime/issues/102924

                filterAndPayloadSpecs.AppendLine($"[AS]{activitySource}/Stop:-TraceId;SpanId;ParentSpanId;ActivityTraceFlags;Kind;DisplayName;StartTimeTicks=StartTimeUtc.Ticks;DurationTicks=Duration.Ticks;Status;StatusDescription;Tags=TagObjects.*Enumerate;ActivitySourceVersion=Source.Version");
            }

            return new[] {
                new EventPipeProvider(
                    DiagnosticSourceEventSource,
                    keywords: DiagnosticSourceEventSourceEvents | DiagnosticSourceEventSourceMessages,
                    eventLevel: EventLevel.Verbose,
                    arguments: new Dictionary<string, string>()
                    {
                        { "FilterAndPayloadSpecs", filterAndPayloadSpecs.ToString() },
                    })
            };
        }
    }
}
