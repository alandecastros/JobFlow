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
    IJobCancellationTokenManager jobCancellationTokenManager,
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
                CancellationToken? jobSpecificToken = null;
                CancellationTokenSource? linkedCts = null;

                try
                {
                    acquiredJob = await storageService.GetNextJobAsync(
                        queueName,
                        workerId,
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        stoppingToken
                    );

                    if (acquiredJob == null)
                    {
                        continue;
                    }

                    Type? payloadType = null;

                    foreach (var assembly in assemblies)
                    {
                        payloadType = assembly.GetType(acquiredJob.PayloadType);
                        if (payloadType != null)
                        {
                            break;
                        }
                    }

                    if (payloadType == null)
                    {
                        await storageService.MarkJobAsFailedById(
                            acquiredJob.Id,
                            Result
                                .Fail($"{workerId} can't find payload type {payloadType}")
                                .ToResultDto(),
                            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                            cancellationToken: stoppingToken
                        );

                        continue;
                    }

                    var payload = JsonSerializerUtils.Deserialize(acquiredJob.Payload, payloadType);

                    if (payload == null)
                    {
                        await storageService.MarkJobAsFailedById(
                            acquiredJob.Id,
                            Result.Fail($"{workerId} can't deserialize the payload").ToResultDto(),
                            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                            cancellationToken: stoppingToken
                        );
                    }

                    jobSpecificToken = jobCancellationTokenManager.RegisterJobTokenSource(
                        acquiredJob.Id
                    );

                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        jobSpecificToken.Value,
                        stoppingToken
                    );

                    var result = await jobHandlerCaller.CallHandler(
                        payloadType,
                        payload,
                        linkedCts.Token
                    );

                    var resultDto = JobHandlerResultUtils.ToResultDto(result);

                    await storageService.MarkJobAsCompletedById(
                        acquiredJob.Id,
                        resultDto,
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        stoppingToken
                    );
                }
                catch (OperationCanceledException ex)
                {
                    if (jobSpecificToken?.IsCancellationRequested == true)
                    {
                        logger.LogInformation(
                            "Operation was cancelled due to jobSpecificToken for job '{JobId}' in queue '{QueueName}'.",
                            acquiredJob!.Id,
                            queueName
                        );

                        await storageService.MarkJobAsStoppedById(
                            acquiredJob!.Id,
                            CancellationToken.None
                        );
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        logger.LogInformation(
                            "Operation was cancelled due to stoppingToken for job '{JobId}' in queue '{QueueName}'.",
                            acquiredJob?.Id,
                            queueName
                        );

                        if (acquiredJob != null)
                        {
                            await MarkJobAsFailedAsync(
                                storageService,
                                acquiredJob.Id,
                                ex,
                                CancellationToken.None
                            );
                        }

                        throw; // Rethrow if the service itself is stopping
                    }
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
                            CancellationToken.None
                        );
                    }
                }
                finally
                {
                    linkedCts?.Dispose();

                    if (acquiredJob?.Id != null)
                    {
                        jobCancellationTokenManager.UnregisterJobTokenSource(acquiredJob.Id);
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

        jobCancellationTokenManager.CancelAllJobTokenSources();

        await storageService.MarkWorkerProcessingJobsAsPending(workerId, cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private async Task MarkJobAsFailedAsync(
        IStorageService storageService,
        string jobId,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        await storageService.MarkJobAsFailedById(
            jobId,
            Result.Fail("Unexpected error").ToResultDto(),
            exception,
            cancellationToken
        );
    }
}
