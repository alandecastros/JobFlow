using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;

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

    public async Task StopJobAsync(string jobId, CancellationToken ct = default)
    {
        await storageService.StopJobAsync(jobId, ct);
    }

    public Task<Job?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        return storageService.GetJobAsync(jobId, ct);
    }
}
