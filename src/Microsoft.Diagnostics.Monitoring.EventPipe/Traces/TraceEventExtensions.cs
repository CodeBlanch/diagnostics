// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal static partial class TraceEventExtensions
    {
        private static readonly Dictionary<string, ActivitySource> s_Sources = new(StringComparer.OrdinalIgnoreCase);
        private static readonly FieldInfo s_ActivityTraceIdFieldInfo = typeof(Activity).GetField("_traceId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NotSupportedException("Activity _traceId field could not be found reflectively.");
        private static readonly FieldInfo s_ActivitySpanIdFieldInfo = typeof(Activity).GetField("_spanId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NotSupportedException("Activity _spanId field could not be found reflectively.");
        private static readonly FieldInfo s_ActivityParentSpanIdFieldInfo = typeof(Activity).GetField("_parentSpanId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new NotSupportedException("Activity _parentSpanId field could not be found reflectively.");
        private static readonly Action<Activity, ActivityKind> s_ActivityKindPropertySetAction = (Action<Activity, ActivityKind>)Delegate.CreateDelegate(
            typeof(Action<Activity, ActivityKind>),
            typeof(Activity).GetProperty("Kind", BindingFlags.Instance | BindingFlags.Public)?.SetMethod ?? throw new NotSupportedException("Activity Kind property set method could not be found reflectively."));
        private static readonly Action<Activity, ActivitySource> s_ActivitySourcePropertySetAction = (Action<Activity, ActivitySource>)Delegate.CreateDelegate(
            typeof(Action<Activity, ActivitySource>),
            typeof(Activity).GetProperty("Source", BindingFlags.Instance | BindingFlags.Public)?.SetMethod ?? throw new NotSupportedException("Activity Source property set method could not be found reflectively."));

        public static bool TryGetActivityPayload(this TraceEvent traceEvent, [NotNullWhen(true)] out Activity? payload)
        {
            if ("Activity/Stop".Equals(traceEvent.EventName))
            {
                string? sourceName = traceEvent.PayloadValue(0) as string;
                string? activityName = traceEvent.PayloadValue(1) as string;
                Array? arguments = traceEvent.PayloadValue(2) as Array;

                if (string.IsNullOrEmpty(activityName)
                    || arguments == null)
                {
                    payload = null;
                    return false;
                }

                payload = new Activity(activityName);

                payload.ActivityTraceFlags = ActivityTraceFlags.Recorded;

                string? status = null;
                string? statusDescription = null;

                foreach (IDictionary<string, object> arg in arguments)
                {
                    string? key = arg["Key"] as string;
                    object value = arg["Value"];

                    switch (key)
                    {
                        case "TraceId":
                            string? traceId = value as string;
                            if (!string.IsNullOrEmpty(traceId)
                                && traceId != "00000000000000000000000000000000")
                            {
                                s_ActivityTraceIdFieldInfo.SetValue(payload, traceId);
                            }
                            break;
                        case "SpanId":
                            string? spanId = value as string;
                            if (!string.IsNullOrEmpty(spanId)
                                && spanId != "0000000000000000")
                            {
                                s_ActivitySpanIdFieldInfo.SetValue(payload, spanId);
                            }
                            break;
                        case "ParentSpanId":
                            string? parentSpanID = value as string;
                            if (!string.IsNullOrEmpty(parentSpanID)
                                && parentSpanID != "0000000000000000")
                            {
                                s_ActivityParentSpanIdFieldInfo.SetValue(payload, parentSpanID);
                            }
                            break;
                        case "Kind":
                            s_ActivityKindPropertySetAction(payload, (ActivityKind)Enum.Parse(typeof(ActivityKind), value as string));
                            break;
                        case "DisplayName":
                            string? displayName = value as string;
                            if (!string.IsNullOrEmpty(displayName)
                                && displayName != activityName)
                            {
                                payload.DisplayName = displayName;
                            }

                            break;
                        case "StartTimeTicks":
                            payload.SetStartTime(DateTime.SpecifyKind(new DateTime(long.Parse(value as string)), DateTimeKind.Utc));
                            break;
                        case "DurationTicks":
                            payload.SetEndTime(payload.StartTimeUtc.AddTicks(long.Parse(value as string)));
                            break;
                        case "Status":
                            status = value as string;
                            break;
                        case "StatusDescription":
                            statusDescription = value as string;
                            break;
                        case "Tags":
                            string? tags = value as string;
                            if (!string.IsNullOrEmpty(tags))
                            {
                                ParseTags(payload, tags);
                            }
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(status))
                {
                    payload.SetStatus(
                        (ActivityStatusCode)Enum.Parse(typeof(ActivityStatusCode), status),
                        statusDescription);
                }

                if (!string.IsNullOrEmpty(sourceName))
                {
                    if (!s_Sources.TryGetValue(sourceName, out ActivitySource activitySource))
                    {
                        activitySource = new(sourceName);
                        s_Sources[sourceName] = activitySource;
                    }

                    s_ActivitySourcePropertySetAction(payload, activitySource);
                }

                payload.Stop();

                return true;
            }

            payload = null;
            return false;
        }

        private static void ParseTags(Activity payload, string tags)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i++] != '[')
                {
                    break;
                }

                int commaPosition = tags.IndexOf(',', i);
                if (commaPosition < 0)
                {
                    break;
                }

                string key = tags.Substring(i, commaPosition - i);

                i = commaPosition + 2;

                int endPosition = tags.IndexOf("]", i);
                if (endPosition < 0)
                {
                    break;
                }

                string value = tags.Substring(i, endPosition - i);

                i = endPosition + 1;

                payload.SetTag(key, value);
            }
        }
    }
}
