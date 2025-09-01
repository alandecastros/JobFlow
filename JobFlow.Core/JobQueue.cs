using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;

namespace JobFlow.Core;

public class JobQueue(
    IStorageService storageService,
    IJobCancellationTokenManager jobCancellationTokenManager,
    JobFlowOptions options
) : IJobQueue
{
    public async Task<string> SubmitJobAsync<T>(
        T request,
        string? queue = null,
        CancellationToken cancellationToken = default
    )
        where T : notnull
    {
        var jobId = await storageService.InsertAsync(
            request,
            queue ?? "default",
            cancellationToken: cancellationToken
        );

        return jobId;
    }

    public Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var result = jobCancellationTokenManager.RequestJobCancellation(jobId);
        return Task.FromResult(result);
    }

    public async Task<bool> RestartJobAsync(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        jobCancellationTokenManager.RequestJobCancellation(jobId);

        while (true)
        {
            var job = await storageService.GetJobAsync(jobId, cancellationToken);
            if (job is { Status: JobStatus.Stopped } or { Status: JobStatus.Completed })
                break;

            var pollingInterval = options.Worker!.PollingIntervalInMilliseconds!.Value * 2;

            await Task.Delay(pollingInterval, cancellationToken);
        }

        await storageService.SetJobAsPending(jobId, cancellationToken);
        return true;
    }

    public Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return storageService.GetJobAsync(jobId, cancellationToken);
    }

    public async Task<Job<T?>?> GetJobAsync<T>(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        var job = await storageService.GetJobAsync(jobId, cancellationToken);

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

    public async Task SetJobDataAsync(
        string jobId,
        object? data,
        CancellationToken cancellationToken = default
    )
    {
        var serializedData = data is not null ? JsonSerializerUtils.Serialize(data) : null;
        await storageService.SetJobData(jobId, serializedData, cancellationToken);
    }
}
