using System.Collections.Generic;
using System.Threading.Tasks;
using JobFlow.Core;
using JobFlow.MongoDb;
using JobFlow.Tests.JobHandlers;
using Microsoft.Extensions.Hosting;

namespace JobFlow.Tests;

public static class MongoDbHost
{
    public static async Task<IHost> CreateAsync(
        string mongoDbConnectionString,
        string database,
        string collection
    )
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    services.AddJobFlow(x =>
                    {
                        x.UseMongoDb(
                            new MongoDbOptions
                            {
                                ConnectionString = mongoDbConnectionString,
                                Database = database,
                                Collection = collection,
                            }
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
