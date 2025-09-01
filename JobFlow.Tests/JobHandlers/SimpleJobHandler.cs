using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

namespace JobFlow.Tests.JobHandlers;

public class SimpleJob : IJob
{
    public int Delay { get; set; } = 100;
}

public class SimpleJobHandler : IJobHandler<SimpleJob>
{
    public async Task HandleJobAsync(
        string jobId,
        SimpleJob message,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(message.Delay, cancellationToken);
    }
}
