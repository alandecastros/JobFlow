using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobFlow.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobFlow(
        this IServiceCollection services,
        Action<JobFlowQueueOptions> configure
    )
    {
        services.AddScoped<IJobQueue, JobQueue>();
        var options = new JobFlowQueueOptions(services);
        configure(options);

        if (options.Worker is null)
            return services;

        var totalHandlers = RegisterJobHandlersFromAssemblies(services, options.Worker.Assemblies);

        services.AddSingleton<IChannelManager, ChannelManager>();
        services.AddSingleton<IJobHandlerCaller, JobHandlerCaller>();
        services.AddSingleton<IJobCancellationTokenManager, JobCancellationTokenManager>();

        services.AddHostedService<JobPollingBackgroundService>();

        services.AddSingleton(options.Worker);

        var queues = options.Worker.Queues ?? new Dictionary<string, JobFlowWorkerQueueOptions>();

        var serviceProvider = services.BuildServiceProvider();
        var channelManager = serviceProvider.GetRequiredService<IChannelManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<JobPollingBackgroundService>>();

        foreach (var (queueName, queueOption) in queues)
        {
            channelManager.GetOrCreateChannel(queueName);

            var numberOfConsumers = queueOption.NumberOfWorkers;

            for (var i = 0; i < numberOfConsumers; i++)
            {
                services.AddSingleton<IHostedService>(provider => new JobWorkerBackgroundService(
                    queueName,
                    options.Worker.WorkerId!,
                    options.Worker.Assemblies,
                    serviceProvider: provider.GetRequiredService<IServiceProvider>(),
                    channelManager: provider.GetRequiredService<IChannelManager>(),
                    jobCancellationTokenManager: provider.GetRequiredService<IJobCancellationTokenManager>(),
                    jobHandlerCaller: provider.GetRequiredService<IJobHandlerCaller>(),
                    logger: provider.GetRequiredService<ILogger<JobWorkerBackgroundService>>()
                ));
            }
        }

        logger.LogInformation($"Total of {totalHandlers} of job handlers registered.");

        return services;
    }

    private static int RegisterJobHandlersFromAssemblies(
        IServiceCollection services,
        Assembly[] assemblies
    )
    {
        var totalHandlers = 0;

        foreach (var assembly in assemblies)
        {
            var jobHandlerImplementations = assembly
                .GetTypes()
                .Where(t =>
                    t.IsClass
                    && !t.IsAbstract
                    && t.GetInterfaces()
                        .Any(i =>
                            i.IsGenericType
                            && (
                                i.GetGenericTypeDefinition() == typeof(IJobHandler<>)
                                || // Handles IJobHandler<T>
                                i.GetGenericTypeDefinition() == typeof(IJobHandler<,>)
                            ) // Handles IJobHandler<T, TR>
                        )
                );

            foreach (var concreteType in jobHandlerImplementations)
            {
                // For each concrete type, find all IJobHandler<T> or IJobHandler<T, TR> interfaces it implements
                var handlerInterfaces = concreteType
                    .GetInterfaces()
                    .Where(i =>
                        i.IsGenericType
                        && (
                            i.GetGenericTypeDefinition() == typeof(IJobHandler<>)
                            || i.GetGenericTypeDefinition() == typeof(IJobHandler<,>)
                        )
                    );

                foreach (var interfaceType in handlerInterfaces)
                {
                    totalHandlers++;
                    services.AddTransient(interfaceType, concreteType);
                }
            }
        }

        return totalHandlers;
    }
}
