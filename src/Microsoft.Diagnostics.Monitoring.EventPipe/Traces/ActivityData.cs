// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal readonly struct ActivityData
    {
        public ActivityData(
            ActivitySourceData? source,
            string operationName,
            string? displayName,
            ActivityKind kind,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
            ActivitySpanId parentSpanId,
            ActivityTraceFlags traceFlags,
            DateTime startTimeUtc,
            DateTime endTimeUtc,
            IReadOnlyList<KeyValuePair<string, string>> tags,
            ActivityStatusCode status,
            string? statusDescription)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            Source = source;
            OperationName = operationName;
            DisplayName = displayName;
            Kind = kind;
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            TraceFlags = traceFlags;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            Status = status;
            StatusDescription = statusDescription;
        }

        public ActivitySourceData? Source { get; }

        public string OperationName { get; }

        public string? DisplayName { get; }

        public ActivityKind Kind { get; }

        public ActivityTraceId TraceId { get; }

        public ActivitySpanId SpanId { get; }

        public ActivitySpanId ParentSpanId { get; }

        public ActivityTraceFlags TraceFlags { get; }

        public DateTime StartTimeUtc { get; }

        public DateTime EndTimeUtc { get; }

        public IReadOnlyList<KeyValuePair<string, string>> Tags { get; }

        public ActivityStatusCode Status { get; }

        public string? StatusDescription { get; }
    }
}
