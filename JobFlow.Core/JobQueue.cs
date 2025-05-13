using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;

namespace JobFlow.Core;

public class JobQueue(IStorageService storageService) : IJobQueue
{
    public async Task<string> SubmitJobAsync<T>(
        T request,
        string? queue = null,
        CancellationToken ct = default
    )
        where T : notnull
    {
        var jobId = await storageService.InsertAsync(
            request,
            queue ?? "default",
            cancellationToken: ct
        );

        return jobId;
    }
}
