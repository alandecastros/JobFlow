using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

namespace JobFlow.Postgres.Tests.JobHandlers;

public class MyResponse
{
    public required string MyId { get; set; }
}

public class MyRequestJob : IJob<MyResponse>
{
    public required string MyId { get; set; }
}

public class MyRequestJobHandler : IJobHandler<MyRequestJob, MyResponse>
{
    public async Task<MyResponse> HandleJobAsync(
        string jobId,
        MyRequestJob message,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(100, cancellationToken);

        return new MyResponse { MyId = message.MyId };
    }
}
