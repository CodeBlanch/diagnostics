// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal static partial class TraceEventExtensions
    {
        private static readonly Dictionary<string, ActivitySourceData> s_Sources = new(StringComparer.OrdinalIgnoreCase);

        public static bool TryGetActivityPayload(this TraceEvent traceEvent, out ActivityData payload)
        {
            if ("Activity/Stop".Equals(traceEvent.EventName))
            {
                string? sourceName = traceEvent.PayloadValue(0) as string;
                string? activityName = traceEvent.PayloadValue(1) as string;
                Array? arguments = traceEvent.PayloadValue(2) as Array;

                if (string.IsNullOrEmpty(activityName)
                    || arguments == null)
                {
                    payload = default;
                    return false;
                }

                ActivitySourceData? source = null;
                string? displayName = null;
                ActivityTraceId traceId = default;
                ActivitySpanId spanId = default;
                ActivitySpanId parentSpanId = default;
                ActivityTraceFlags traceFlags = default;
                ActivityKind kind = default;
                ActivityStatusCode status = ActivityStatusCode.Unset;
                string? statusDescription = null;
                string? sourceVersion = null;
                DateTime startTimeUtc = default;
                long durationTicks = 0;
                IReadOnlyList<KeyValuePair<string, string>>? tags = null;

                foreach (IDictionary<string, object> arg in arguments)
                {
                    string? key = arg["Key"] as string;
                    object value = arg["Value"];

                    switch (key)
                    {
                        case "TraceId":
                            string? traceIdValue = value as string;
                            if (!string.IsNullOrEmpty(traceIdValue)
                                && traceIdValue != "00000000000000000000000000000000")
                            {
                                traceId = ActivityTraceId.CreateFromString(traceIdValue);
                            }
                            break;
                        case "SpanId":
                            string? spanIdValue = value as string;
                            if (!string.IsNullOrEmpty(spanIdValue)
                                && spanIdValue != "0000000000000000")
                            {
                                spanId = ActivitySpanId.CreateFromString(spanIdValue);
                            }
                            break;
                        case "ParentSpanId":
                            string? parentSpanIdValue = value as string;
                            if (!string.IsNullOrEmpty(parentSpanIdValue)
                                && parentSpanIdValue != "0000000000000000")
                            {
                                parentSpanId = ActivitySpanId.CreateFromString(parentSpanIdValue);
                            }
                            break;
                        case "ActivityTraceFlags":
                            traceFlags = (ActivityTraceFlags)Enum.Parse(typeof(ActivityTraceFlags), value as string);
                            break;
                        case "Kind":
                            kind = (ActivityKind)Enum.Parse(typeof(ActivityKind), value as string);
                            break;
                        case "DisplayName":
                            string? displayNameValue = value as string;
                            if (!string.IsNullOrEmpty(displayNameValue)
                                && displayNameValue != activityName)
                            {
                                displayName = displayNameValue;
                            }

                            break;
                        case "StartTimeTicks":
                            startTimeUtc = DateTime.SpecifyKind(new DateTime(long.Parse(value as string)), DateTimeKind.Utc);
                            break;
                        case "DurationTicks":
                            durationTicks = long.Parse(value as string);
                            break;
                        case "Status":
                            status = (ActivityStatusCode)Enum.Parse(typeof(ActivityStatusCode), value as string);
                            break;
                        case "StatusDescription":
                            statusDescription = value as string;
                            break;
                        case "Tags":
                            string? tagsValue = value as string;
                            if (!string.IsNullOrEmpty(tagsValue))
                            {
                                tags = ParseTags(tagsValue);
                            }
                            break;
                        case "ActivitySourceVersion":
                            sourceVersion = value as string;
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(sourceName))
                {
                    if (!s_Sources.TryGetValue(sourceName, out source))
                    {
                        source = new(sourceName, sourceVersion);
                        s_Sources[sourceName] = source;
                    }

                }

                payload = new ActivityData(
                    source,
                    activityName,
                    displayName,
                    kind,
                    traceId,
                    spanId,
                    parentSpanId,
                    traceFlags,
                    startTimeUtc,
                    startTimeUtc + TimeSpan.FromTicks(durationTicks),
                    tags ?? Array.Empty<KeyValuePair<string, string>>(),
                    status,
                    statusDescription);
                return true;
            }

            payload = default;
            return false;
        }

        private static IReadOnlyList<KeyValuePair<string, string>>? ParseTags(string tagsValue)
        {
            List<KeyValuePair<string, string>> tags = new(64);

            for (int i = 0; i < tagsValue.Length; i++)
            {
                if (tagsValue[i++] != '[')
                {
                    break;
                }

                int commaPosition = tagsValue.IndexOf(',', i);
                if (commaPosition < 0)
                {
                    break;
                }

                string key = tagsValue.Substring(i, commaPosition - i);

                i = commaPosition + 2;

                int endPosition = tagsValue.IndexOf("]", i);
                if (endPosition < 0)
                {
                    break;
                }

                string value = tagsValue.Substring(i, endPosition - i);

                i = endPosition + 1;

                tags.Add(new(key, value));
            }

            return tags;
        }
    }
}
