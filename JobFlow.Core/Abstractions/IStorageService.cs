using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IStorageService
{
    Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task SetJobData(string jobId, string? data, CancellationToken cancellationToken = default);
    Task<string> InsertAsync<T>(
        T request,
        string queue,
        CancellationToken cancellationToken = default
    )
        where T : notnull;
    Task<long> GetPendingCountAsync(
        string queueName,
        CancellationToken cancellationToken = default
    );
    Task<long> GetInProgressCountAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    );
    Task<Job?> GetNextJobAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    );
    Task SetJobAsFailed(
        string jobId,
        Exception exception,
        CancellationToken cancellationToken = default
    );
    Task SetJobAsCompleted(
        string jobId,
        string? data,
        CancellationToken cancellationToken = default
    );
    Task SetJobAsPending(string jobId, CancellationToken cancellationToken = default);
    Task SetJobAsStopped(string jobId, CancellationToken cancellationToken = default);
    Task SetWorkerProcessingJobsAsStopped(
        string workerId,
        CancellationToken cancellationToken = default
    );
}
