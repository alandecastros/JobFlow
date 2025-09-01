using System;
using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

namespace JobFlow.Tests.JobHandlers;

public class ToFailJob : IJob { }

public class ToFailJobHandler : IJobHandler<ToFailJob>
{
    public Task HandleJobAsync(string jobId, ToFailJob message, CancellationToken cancellationToken)
    {
        throw new Exception("Unhandled exception");
    }
}
