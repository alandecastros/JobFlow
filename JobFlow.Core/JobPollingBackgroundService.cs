using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobFlow.Core;

public class JobPollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly JobFlowWorkerOptions _workerOptions;
    private readonly ILogger<JobPollingBackgroundService> _logger;
    private readonly IChannelManager _channelManager;
    private readonly TimeSpan _pollingInterval;
    private readonly List<string> _queuesToPoll;
    private readonly Dictionary<string, int> _queueWorkerCounts;

    public JobPollingBackgroundService(
        IServiceProvider serviceProvider,
        JobFlowWorkerOptions workerOptions,
        IChannelManager channelManager,
        ILogger<JobPollingBackgroundService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _workerOptions = workerOptions;
        _logger = logger;
        _channelManager = channelManager;

        _pollingInterval = TimeSpan.FromMilliseconds(
            workerOptions.PollingIntervalInMilliseconds ?? 1000
        );

        _queueWorkerCounts = new Dictionary<string, int>();
        var configuredQueues = workerOptions.Queues!;

        foreach (var kvp in configuredQueues)
        {
            var workerCount = Math.Max(1, kvp.Value.NumberOfWorkers);
            _queueWorkerCounts[kvp.Key] = workerCount;

            _logger.LogDebug(
                "Queue '{QueueName}' configured with {WorkerCount} workers.",
                kvp.Key,
                workerCount
            );
        }

        _queuesToPoll = _queueWorkerCounts.Keys.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        using var scope = _serviceProvider.CreateScope();

        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Starting polling cycle at {PollingStartTime}.", DateTime.UtcNow);

            foreach (var queueName in _queuesToPoll)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                var workerCount = _queueWorkerCounts[queueName];

                try
                {
                    var totalJobsPending = await storageService.GetPendingCountAsync(
                        queueName,
                        stoppingToken
                    );

                    var totalJobsInProgress = await storageService.GetInProgressCountAsync(
                        queueName,
                        _workerOptions.WorkerId!,
                        stoppingToken
                    );

                    var freeWorkerCount = workerCount - totalJobsInProgress;

                    if (freeWorkerCount > 0)
                    {
                        var totalJobsToPublish = Math.Min(freeWorkerCount, totalJobsPending);
                        var writer = _channelManager.GetWriter(queueName);

                        for (var i = 0; i < totalJobsToPublish; i++)
                        {
                            await writer.WriteAsync(new object(), stoppingToken);
                        }

                        _logger.LogDebug(
                            "Worker {workerId} will start to execute {totalJobsToPublish} jobs from queue '{queueName}'.",
                            _workerOptions.WorkerId,
                            totalJobsToPublish,
                            queueName
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Polling operation cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during polling cycle.");
                    break;
                }
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }
}
