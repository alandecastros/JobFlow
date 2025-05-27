using System;
using System.Collections.Generic;
using System.Linq;
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

    public async Task<IList<string>> GetRequestedToStopJobsIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var filter =
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Processing)
            & Builders<Job>.Filter.Ne(j => j.StoppedAt, null);

        var projection = Builders<Job>.Projection.Include(j => j.Id);

        var jobsToUpdateQuery = await _jobCollection
            .Find(filter)
            .Project<Job>(projection) // Project to Job, Id will be populated
            .ToListAsync(cancellationToken);

        return jobsToUpdateQuery.Select(x => x.Id).ToList();
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
            Status = 1,
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
            .Update.Set(j => j.StoppedAt, utcNow)
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

    public async Task MarkJobAsFailedById(
        string jobId,
        string errorMessage,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        var resultSerialized = JsonSerializerUtils.Serialize(errorMessage);

        var failedFilter = Builders<Job>.Filter.Eq(j => j.Id, jobId);
        var failedUpdate = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Failed)
            .Set(j => j.Results, resultSerialized)
            .Set(j => j.StackTrace, exception?.ToString())
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(
            failedFilter,
            failedUpdate,
            cancellationToken: cancellationToken // Pass the token
        );
    }

    public async Task MarkJobAsCompletedById(
        string jobId,
        object? results,
        CancellationToken cancellationToken = default
    )
    {
        var resultSerialized = results is not null ? JsonSerializerUtils.Serialize(results) : null;

        var completeFilter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var completeUpdate = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Completed)
            .Set(j => j.Results, resultSerialized)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(
            completeFilter,
            completeUpdate,
            cancellationToken: cancellationToken
        );
    }

    public async Task MarkJobAsPendingById(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Pending)
            .Set(j => j.WorkerId, null)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task MarkJobAsStoppedById(
        string jobId,
        CancellationToken cancellationToken = default
    )
    {
        var filter = Builders<Job>.Filter.Eq(j => j.Id, jobId);

        var update = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Stopped)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task MarkWorkerProcessingJobsAsStopped(
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var currentProcessingByThisWorkerFilter =
            Builders<Job>.Filter.Eq(j => j.Status, JobStatus.Processing)
            & Builders<Job>.Filter.Eq(j => j.WorkerId, workerId);

        var backToPendingUpdate = Builders<Job>
            .Update.Set(j => j.Status, JobStatus.Pending)
            .Set(j => j.WorkerId, null)
            .Set(j => j.UpdatedAt, DateTime.UtcNow);

        await _jobCollection.UpdateOneAsync(
            currentProcessingByThisWorkerFilter,
            backToPendingUpdate,
            cancellationToken: cancellationToken
        );
    }
}
