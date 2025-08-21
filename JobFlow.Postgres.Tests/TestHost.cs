using System.Collections.Generic;
using System.Threading.Tasks;
using JobFlow.Core;
using JobFlow.Postgres.Tests.JobHandlers;
using Microsoft.Extensions.Hosting;

namespace JobFlow.Postgres.Tests;

public static class TestHost
{
    public static async Task<IHost> CreateAsync(string postgresConnectionString)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    services.AddJobFlow(x =>
                    {
                        x.UsePostgres(
                            new PostgresOptions { ConnectionString = postgresConnectionString }
                        );

                        x.Worker = new JobFlowWorkerOptions
                        {
                            Assemblies = [typeof(SimpleJob).Assembly],
                            WorkerId = "TestHost",
                            PollingIntervalInMilliseconds = 500,
                            Queues = new Dictionary<string, JobFlowWorkerQueueOptions>
                            {
                                {
                                    "default",
                                    new JobFlowWorkerQueueOptions { NumberOfWorkers = 100 }
                                },
                            },
                        };
                    });
                }
            )
            .Build();

        await host.StartAsync();
        return host;
    }
}
