using System;
using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UUIDNext;

namespace JobFlow.Postgres;

public class PostgresStorageService(
    [FromKeyedServices("postgres-job-queue")] NpgsqlDataSource dataSource,
    PostgresOptions postgresOptions
) : IStorageService
{
    public async Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var command = dataSource.CreateCommand(
            $"""
            SELECT 
                id, 
                queue, 
                status, 
                payload, 
                payload_type, 
                data, 
                created_at, 
                updated_at, 
                exception_message,
                exception_stacktrace
            FROM {postgresOptions.TableName}
            WHERE id = @jobId
            """
        );

        command.Parameters.AddWithValue("jobId", new Guid(jobId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetGuid(0);
        var queue = reader.GetString(1);
        var status = reader.GetInt32(2);
        var payload = reader.GetString(3);
        var payloadType = reader.GetString(4);
        var data = reader.IsDBNull(5) ? null : reader.GetString(5);
        var createdAt = reader.GetDateTime(6);
        var updatedAt = reader.GetDateTime(7);
        var exceptionMessage = reader.IsDBNull(8) ? null : reader.GetString(8);
        var exceptionStacktrace = reader.IsDBNull(9) ? null : reader.GetString(9);

        return new Job
        {
            Id = id.ToString(),
            Queue = queue,
            Status = status,
            Payload = payload,
            PayloadType = payloadType,
            Data = data,
            ExceptionMessage = exceptionMessage,
            ExceptionStacktrace = exceptionStacktrace,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    public async Task SetJobData(
        string jobId,
        string? data,
        CancellationToken cancellationToken = default
    )
    {
        var command = dataSource.CreateCommand(
            $"UPDATE {postgresOptions.TableName} SET data = @data, updated_at = now() WHERE id = @id"
        );
        command.Parameters.AddWithValue("data", data ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("id", Guid.Parse(jobId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> InsertAsync<T>(
        T request,
        string queue,
        CancellationToken cancellationToken = default
    )
        where T : notnull
    {
        var jobId = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        var payload = JsonSerializerUtils.Serialize(request);
        var payloadType = request.GetType().FullName!;

        await using var command = dataSource.CreateCommand(
            $"""
             INSERT INTO {postgresOptions.TableName}
             (
                 id, 
                 status, 
                 queue, 
                 payload, 
                 payload_type, 
                 created_at, 
                 updated_at
             ) 
             VALUES 
             (
                  @id, 
                  {JobStatus.Pending}, 
                  @queue, 
                  @payload, 
                  @payload_type, 
                  now(), 
                  now()
            )
            """
        );

        command.Parameters.AddWithValue("id", jobId);
        command.Parameters.AddWithValue("queue", queue);
        command.Parameters.AddWithValue("payload", payload);
        command.Parameters.AddWithValue("payload_type", payloadType);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return jobId.ToString();
    }

    public async Task<long> GetPendingCountAsync(
        string queueName,
        CancellationToken cancellationToken = default
    )
    {
        await using var cmdJobsPending = dataSource.CreateCommand(
            $"""
            SELECT COUNT(1) 
            FROM {postgresOptions.TableName} 
            WHERE status = {JobStatus.Pending} and queue = @queue
            """
        );

        cmdJobsPending.Parameters.AddWithValue("queue", queueName);

        var cmdJobsPendingResult = await cmdJobsPending.ExecuteScalarAsync(cancellationToken);

        var totalJobsPending = Convert.ToInt64(cmdJobsPendingResult);

        return totalJobsPending;
    }

    public async Task<long> GetInProgressCountAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        await using var cmdJobsInProgress = dataSource.CreateCommand(
            $"""
            SELECT COUNT(1) 
            FROM {postgresOptions.TableName} 
            WHERE status = {JobStatus.Processing} and queue = @queue and worker_id = @workerId
            """
        );

        cmdJobsInProgress.Parameters.AddWithValue("queue", queueName);
        cmdJobsInProgress.Parameters.AddWithValue("workerId", workerId);

        var cmdJobsInProgressResult = await cmdJobsInProgress.ExecuteScalarAsync(cancellationToken);

        var totalJobsInProgress = Convert.ToInt64(cmdJobsInProgressResult);

        return totalJobsInProgress;
    }

    public async Task<Job?> GetNextJobAsync(
        string queueName,
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var command = dataSource.CreateCommand(
            $"""
                BEGIN;
                WITH cte_job AS (
                    SELECT id
                    FROM {postgresOptions.TableName}
                    WHERE status = {JobStatus.Pending} and queue = @queue
                    ORDER BY id
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                )
                UPDATE {postgresOptions.TableName}
                SET status = {JobStatus.Processing},
                    worker_id = @workerId,
                    updated_at = now()
                FROM cte_job
                WHERE {postgresOptions.TableName}.id = cte_job.id
                RETURNING {postgresOptions.TableName}.id, 
                    {postgresOptions.TableName}.payload, 
                    {postgresOptions.TableName}.payload_type, 
                    {postgresOptions.TableName}.status, 
                    {postgresOptions.TableName}.queue;
                COMMIT;
            """
        );

        command.Parameters.AddWithValue("queue", queueName);
        command.Parameters.AddWithValue("workerId", workerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);

        var id = reader.GetGuid(0);
        var payload = reader.GetString(1);
        var payloadType = reader.GetString(2);
        var status = reader.GetInt32(3);
        var queue = reader.GetString(4);

        return new Job
        {
            Id = id.ToString(),
            Payload = payload,
            PayloadType = payloadType,
            Status = status,
            Queue = queue,
        };
    }

    public async Task SetJobAsFailed(
        string jobId,
        Exception exception,
        CancellationToken cancellationToken = default
    )
    {
        var command = dataSource.CreateCommand(
            $"""
            UPDATE {postgresOptions.TableName} 
            SET 
                status = {JobStatus.Failed}, 
                exception_message = @exceptionMessage, 
                exception_stacktrace = @exceptionStacktrace,
                updated_at = now() 
            WHERE id = @id
            """
        );
        command.Parameters.AddWithValue("exceptionMessage", exception.Message);
        command.Parameters.AddWithValue(
            "exceptionStacktrace",
            exception.StackTrace ?? (object)DBNull.Value
        );
        command.Parameters.AddWithValue("id", Guid.Parse(jobId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetJobAsCompleted(
        string jobId,
        string? data,
        CancellationToken cancellationToken = default
    )
    {
        var command = dataSource.CreateCommand(
            $"""
            UPDATE {postgresOptions.TableName} 
            SET 
                status = {JobStatus.Completed}, 
                data = @data, 
                updated_at = now() 
            WHERE id = @id
            """
        );

        command.Parameters.AddWithValue("data", data is not null ? data : DBNull.Value);
        command.Parameters.AddWithValue("id", Guid.Parse(jobId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetJobAsPending(string jobId, CancellationToken cancellationToken = default)
    {
        var command = dataSource.CreateCommand(
            $"""
            UPDATE {postgresOptions.TableName} 
            SET 
                status = {JobStatus.Pending}, 
                worker_id = null, 
                data = null, 
                exception_message = null, 
                exception_stacktrace = null,
                updated_at = now() 
            WHERE id = @id
            """
        );
        command.Parameters.AddWithValue("id", Guid.Parse(jobId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetJobAsStopped(string jobId, CancellationToken cancellationToken = default)
    {
        var command = dataSource.CreateCommand(
            $"""
            UPDATE {postgresOptions.TableName} 
            SET 
                status = {JobStatus.Stopped}, 
                updated_at = now() 
            WHERE id = @id
            """
        );
        command.Parameters.AddWithValue("id", Guid.Parse(jobId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetWorkerProcessingJobsAsStopped(
        string workerId,
        CancellationToken cancellationToken = default
    )
    {
        var command = dataSource.CreateCommand(
            $"""
            UPDATE {postgresOptions.TableName} 
            SET 
                status = {JobStatus.Stopped}, 
                updated_at = now() 
            WHERE worker_id = @workerId AND status = {JobStatus.Processing}
            """
        );
        command.Parameters.AddWithValue("workerId", workerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
