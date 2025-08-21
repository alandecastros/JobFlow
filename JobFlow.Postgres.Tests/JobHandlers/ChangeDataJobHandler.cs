using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

namespace JobFlow.Postgres.Tests.JobHandlers;

public class ProgressData
{
    public required int Progress { get; set; }
}

public class ChangeDataJob : IJob { }

public class ChangeDataJobHandler(IJobQueue jobQueue) : IJobHandler<ChangeDataJob>
{
    public async Task HandleJobAsync(
        string jobId,
        ChangeDataJob message,
        CancellationToken cancellationToken
    )
    {
        await jobQueue.SetJobDataAsync(
            jobId,
            new ProgressData { Progress = 50 },
            cancellationToken
        );

        await Task.Delay(1000, cancellationToken);
    }
}
