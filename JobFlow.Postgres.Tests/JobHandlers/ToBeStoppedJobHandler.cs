using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

namespace JobFlow.Postgres.Tests.JobHandlers;

public class ToBeStoppedJob : IJob { }

public class ToBeStoppedJobHandler : IJobHandler<ToBeStoppedJob>
{
    public async Task HandleJobAsync(
        string jobId,
        ToBeStoppedJob message,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(60000, cancellationToken);
    }
}
