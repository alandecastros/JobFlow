using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;

namespace JobFlow.Core;

public class JobQueue(
    IStorageService storageService,
    IJobCancellationTokenManager jobCancellationTokenManager
) : IJobQueue
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

    public Task<bool> StopJobAsync(string jobId, CancellationToken ct = default)
    {
        var result = jobCancellationTokenManager.RequestJobCancellation(jobId);
        return Task.FromResult(result);
    }

    public async Task<bool> RestartJobAsync(string jobId, CancellationToken ct = default)
    {
        var result = jobCancellationTokenManager.RequestJobCancellation(jobId);

        if (!result)
        {
            return false;
        }

        while (await storageService.GetJobAsync(jobId, ct) is not { Status: JobStatus.Stopped })
        {
            await Task.Delay(1000, ct);
        }

        await storageService.SetJobAsPending(jobId, ct);
        return true;
    }

    public Task<Job?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        return storageService.GetJobAsync(jobId, ct);
    }

    public async Task<Job<T?>?> GetJobAsync<T>(string jobId, CancellationToken ct = default)
    {
        var job = await storageService.GetJobAsync(jobId, ct);

        if (job is null)
        {
            return null;
        }

        var data = job.Data is not null ? JsonSerializerUtils.Deserialize<T?>(job.Data) : default;

        return new Job<T?>
        {
            Id = job.Id,
            Queue = job.Queue,
            Status = job.Status,
            Payload = job.Payload,
            PayloadType = job.PayloadType,
            Data = data,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
        };
    }

    public async Task SetJobDataAsync(string jobId, object? data, CancellationToken ct = default)
    {
        var serializedData = data is not null ? JsonSerializerUtils.Serialize(data) : null;
        await storageService.SetJobData(jobId, serializedData, ct);
    }
}
