// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal interface IActivityLogger
    {
        void Log(Activity activity);

        Task PipelineStarted(CancellationToken token);
        Task PipelineStopped(CancellationToken token);
    }
}
