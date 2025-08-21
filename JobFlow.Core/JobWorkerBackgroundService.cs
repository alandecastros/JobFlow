using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

                Abstractions.Job? acquiredJob = null;
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
                        throw new JobPayloadTypeNotFoundException(workerId, queueName);
                    }

                    var payload = JsonSerializerUtils.Deserialize(acquiredJob.Payload, payloadType);

                    jobSpecificToken = jobCancellationTokenManager.RegisterJobTokenSource(
                        acquiredJob.Id
                    );

                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        jobSpecificToken.Value,
                        stoppingToken
                    );

                    var data = await jobHandlerCaller.CallHandler(
                        acquiredJob.Id,
                        payloadType,
                        payload,
                        linkedCts.Token
                    );

                    var dataSerialized = data is not null
                        ? JsonSerializerUtils.Serialize(data)
                        : null;

                    await storageService.SetJobAsCompleted(
                        acquiredJob.Id,
                        dataSerialized,
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

                        await storageService.SetJobAsStopped(
                            acquiredJob.Id,
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
                            await storageService.SetJobAsFailed(
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
                        await storageService.SetJobAsFailed(
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
            logger.LogError(ex, ex.Message);
            jobCancellationTokenManager.CancelAllJobTokenSources();

            using var scope = serviceProvider.CreateScope();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            await storageService.SetWorkerProcessingJobsAsStopped(workerId, CancellationToken.None);

            if (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Consumer service for queue '{QueueName}' stopped due to an unexpected error.",
                    queueName
                );
            }
        }
    }
}
