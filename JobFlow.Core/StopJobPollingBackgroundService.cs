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

public class StopJobPollingBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<StopJobPollingBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();

        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var jobCancellationTokenManager =
            scope.ServiceProvider.GetRequiredService<IJobCancellationTokenManager>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var jobsIds = await storageService.GetRequestedToStopJobsIdsAsync(stoppingToken);

            foreach (var jobId in jobsIds)
            {
                jobCancellationTokenManager.RequestJobCancellation(jobId);
                jobCancellationTokenManager.UnregisterJobTokenSource(jobId);
            }

            await Task.Delay(100, stoppingToken);
        }
    }
}
