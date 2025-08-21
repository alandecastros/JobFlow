using System;
using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace JobFlow.MongoDb;

public class MongoDbStorageService : IStorageService
{
    private readonly IMongoCollection<Job> _jobCollection;

    public MongoDbStorageService(
        [FromKeyedServices("mongodb-job-queue")] IMongoClient mongoClient,
        MongoDbOptions mongoDbOptions
    )
    {
        var database = mongoClient.GetDatabase(mongoDbOptions.Database);
        _jobCollection = database.GetCollection<Job>(mongoDbOptions.Collection);
    }

    public async Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var job = await _jobCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);

        return job;
    }

    public async Task SetJobData(
        string jobId,
        string? data,
        CancellationToken cancellationToken = default
    )
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Data, data)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<string> InsertAsync<T>(
        T request,
        string queue,
        CancellationToken cancellationToken = default
    )
        where T : notnull
    {
        var payload = JsonSerializerUtils.Serialize(request);
        var payloadType = request.GetType().FullName!;

        var job = new Job
        {
            Status = JobStatus.Pending,
            Queue = queue,
            Payload = payload,
            PayloadType = payloadType,
        };

        await _jobCollection.InsertOneAsync(job, cancellationToken: cancellationToken);

        return job.Id;
    }

    public async Task StopJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var utcNow = DateTime.UtcNow;

        var update = Builders<Job>
            .Update.Set(x => x.Status, JobStatus.Stopped)
            .Set(j => j.UpdatedAt, utcNow);

        await _jobCollection.FindOneAndUpdateAsync(
            filter,
            update,
            cancellationToken: cancellationToken
        );
    }

    public async Task<long> GetPendingCountAsync(
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        var filterPendingJobs = Builders<Job>.Filter.And(
            Builders<Job>.Filter.Eq(j => j.Queue, queueName),
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Pending)
        );

        var totalJobsPending = await _jobCollection.CountDocumentsAsync(
            filterPendingJobs,
            cancellationToken: cancellationToken
        );

        return totalJobsPending;
    }

    public async Task<long> GetInProgressCountAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var filterInProgressJobs = Builders<Job>.Filter.And(
            Builders<Job>.Filter.Eq(j => j.Queue, queueName),
            Builders<Job>.Filter.Eq(j => j.WorkerId, workerId),
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Processing)
        );

        var totalJobsInProgress = await _jobCollection.CountDocumentsAsync(
            filterInProgressJobs,
            cancellationToken: cancellationToken
        );

        return totalJobsInProgress;
    }

    public async Task<Job?> GetNextJobAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var filter =
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Pending)
            & Builders<Job>.Filter.Eq(j => j.Queue, queueName);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Processing)
            .Set(j => j.WorkerId, workerId)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<Job>
        {
            ReturnDocument = ReturnDocument.After,
            Sort = Builders<Job>.Sort.Ascending(j => j.CreatedAt),
        };

        var acquiredJob = await _jobCollection.FindOneAndUpdateAsync(
            filter,
            update,
            options,
            cancellationToken
        );

        return acquiredJob;
    }

    public async Task SetJobAsFailed(
        string jobId,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);
        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Failed)
            .Set(j => j.ExceptionMessage, exception.Message)
            .Set(j => j.ExceptionStacktrace, exception.StackTrace)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken // Pass the token
        );
    }

    public async Task SetJobAsCompleted(
        string jobId,
        string? data,
        CancellationToken cancellationToken = default
    )
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Completed)
            .Set(j => j.Data, data)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task SetJobAsPending(string jobId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Pending)
            .Set(j => j.WorkerId, null)
            .Set(j => j.Data, null)
            .Set(j => j.ExceptionMessage, null)
            .Set(j => j.ExceptionStacktrace, null)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task SetJobAsStopped(string jobId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Stopped)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task SetWorkerProcessingJobsAsStopped(
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var currentProcessingByThisWorkerFilter =
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Processing)
            & Builders<Job>.Filter.Eq(j => j.WorkerId, workerId);

        var backToPendingUpdate = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Stopped)
            .Set(j => j.WorkerId, null)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(
            currentProcessingByThisWorkerFilter,
            backToPendingUpdate,
            cancellationToken: cancellationToken
        );
    }
}
