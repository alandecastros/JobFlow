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
    public static IServiceCollection AddJobFlowCore(
        this IServiceCollection services,
        Assembly[] assemblies
    )
    {
        services.AddSingleton<IChannelManager, ChannelManager>();
        services.AddSingleton<IJobHandlerCaller, JobHandlerCaller>();

        RegisterJobHandlersFromAssemblies(services, assemblies);

        return services;
    }

    public static IServiceCollection AddJobFlowQueue(
        this IServiceCollection services,
        Action<JobFlowQueueOptions> configure
    )
    {
        services.AddScoped<IJobQueue, JobQueue>();
        configure(new JobFlowQueueOptions(services));
        return services;
    }

    public static IServiceCollection AddJobFlowWorker(
        this IServiceCollection services,
        Assembly[] assemblies,
        Action<JobFlowWorkerOptions> configure
    )
    {
        RegisterJobHandlersFromAssemblies(services, assemblies);

        services.AddSingleton<IChannelManager, ChannelManager>();
        services.AddSingleton<IJobHandlerCaller, JobHandlerCaller>();

        services.AddHostedService<JobPollingBackgroundService>();

        var options = new JobFlowWorkerOptions(services);

        configure(options);

        services.AddSingleton(options);

        var queues = options.Queues ?? new Dictionary<string, JobFlowWorkerQueueOptions>();

        if (!queues.ContainsKey("default"))
        {
            queues["default"] = new JobFlowWorkerQueueOptions { NumberOfWorkers = 0 };
        }

        var serviceProvider = services.BuildServiceProvider();
        var channelManager = serviceProvider.GetRequiredService<IChannelManager>();

        foreach (var (queueName, queueOption) in queues)
        {
            channelManager.GetOrCreateChannel(queueName);

            var numberOfConsumers = queueOption.NumberOfWorkers;

            for (var i = 0; i < numberOfConsumers; i++)
            {
                services.AddSingleton<IHostedService>(provider => new JobWorkerBackgroundService(
                    queueName,
                    options.WorkerId!,
                    assemblies,
                    serviceProvider: provider.GetRequiredService<IServiceProvider>(),
                    channelManager: provider.GetRequiredService<IChannelManager>(),
                    jobHandlerCaller: provider.GetRequiredService<IJobHandlerCaller>(),
                    logger: provider.GetRequiredService<ILogger<JobWorkerBackgroundService>>()
                ));
            }
        }

        return services;
    }

    private static void RegisterJobHandlersFromAssemblies(
        IServiceCollection services,
        Assembly[] assemblies
    )
    {
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
                    services.AddTransient(interfaceType, concreteType);
                }
            }
        }
    }
}
