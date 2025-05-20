using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IStorageService
{
    Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    Task<IList<string>> GetRequestedToStopJobsIdsAsync(
        CancellationToken cancellationToken = default
    );

    Task<string> InsertAsync<T>(
        T request,
        string queue,
        CancellationToken cancellationToken = default
    )
        where T : notnull;

    Task StopJobAsync(string jobId, CancellationToken cancellationToken = default);

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

    Task MarkJobAsFailedById(
        string jobId,
        ResultDto resultDto,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    );

    Task MarkJobAsCompletedById(
        string jobId,
        ResultDto resultDto,
        CancellationToken cancellationToken = default
    );

    Task MarkJobAsPendingById(string jobId, CancellationToken cancellationToken = default);

    Task MarkJobAsStoppedById(string jobId, CancellationToken cancellationToken = default);

    Task MarkWorkerProcessingJobsAsPending(
        string workerId,
        CancellationToken cancellationToken = default
    );
}
