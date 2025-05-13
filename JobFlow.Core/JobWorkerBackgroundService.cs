using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobFlow.Core;

public class JobWorkerBackgroundService(
    string queueName,
    string workerId,
    Assembly[] assemblies,
    IServiceProvider serviceProvider,
    IChannelManager channelManager,
    IJobHandlerCaller jobHandlerCaller,
    ILogger<JobWorkerBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = channelManager.GetReader(queueName);

        try
        {
            await foreach (var _ in reader.ReadAllAsync(stoppingToken))
            {
                using var scope = serviceProvider.CreateScope();
                var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

                Job? acquiredJob = null;
                try
                {
                    acquiredJob = await storageService.GetNextJobAsync(
                        queueName,
                        workerId,
                        stoppingToken
                    );

                    if (acquiredJob != null)
                    {
                        logger.LogDebug(
                            "Consumer for queue '{QueueName}' acquired Job ID: {JobId}",
                            queueName,
                            acquiredJob.Id
                        );

                        await ProcessJob(storageService, acquiredJob, stoppingToken);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    logger.LogError(
                        ex,
                        "Operation cancelled error occurred processing signal for queue '{QueueName}'. This will stop the worker.",
                        queueName
                    );

                    if (acquiredJob != null)
                    {
                        await MarkJobAsFailedAsync(
                            storageService,
                            acquiredJob.Id,
                            ex,
                            stoppingToken
                        );
                    }

                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Unhandled error occurred processing signal for queue '{QueueName}'.",
                        queueName
                    );

                    if (acquiredJob != null)
                    {
                        await MarkJobAsFailedAsync(
                            storageService,
                            acquiredJob.Id,
                            ex,
                            stoppingToken
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Consumer service for queue '{QueueName}' stopped due to an unexpected error.",
                queueName
            );
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        await storageService.MarkWorkerProcessingJobsAsPending(workerId, cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessJob(
        IStorageService storageService,
        Job job,
        CancellationToken cancellationToken
    )
    {
        Type? payloadType = null;

        foreach (var assembly in assemblies)
        {
            payloadType = assembly.GetType(job.PayloadType);
            if (payloadType != null)
            {
                break;
            }
        }

        if (payloadType == null)
        {
            logger.LogError(
                "Could not find type '{JobPayloadType}' for Job ID: {JobId}",
                job.PayloadType,
                job.Id
            );

            await storageService.MarkJobAsPendingById(job.Id, cancellationToken);

            return;
        }

        var payload = JsonSerializerUtils.Deserialize(job.Payload, payloadType);

        if (payload == null)
        {
            throw new Exception($"Deserialized payload is null for Job ID: {job.Id}");
        }

        try
        {
            var result = await jobHandlerCaller.CallHandler(
                payloadType,
                job.Payload,
                cancellationToken
            );

            var resultDto = JobHandlerResultUtils.ToResultDto(result);

            await storageService.MarkJobAsCompletedById(job.Id, resultDto, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Job ID: {JobId} with Mediator.", job.Id);

            await MarkJobAsFailedAsync(storageService, job.Id, ex, cancellationToken);
        }
    }

    private async Task MarkJobAsFailedAsync(
        IStorageService storageService,
        string jobId,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Marking job {JobId} as Failed.", jobId);
        try
        {
            await storageService.MarkJobAsFailedById(
                jobId,
                Result.Fail("Unexpected error").ToResultDto(),
                exception,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            // Log specifically that updating the status failed
            logger.LogError(ex, "Failed to update status to Failed for job {JobId}.", jobId);
            // Depending on requirements, you might want to re-throw or handle differently
        }
    }
}
