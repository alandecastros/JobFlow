using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JobFlow.Core;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace JobFlow.Postgres;

public static class JobFlowOptionsExtensions
{
    public static void UsePostgres(
        this JobFlowQueueOptions queueOptions,
        PostgresOptions postgresOptions
    )
    {
        queueOptions.Services.TryAddKeyedSingleton<NpgsqlDataSource>(
            "postgres-job-queue",
            (_, _) => NpgsqlDataSource.Create(postgresOptions.ConnectionString!)
        );

        queueOptions.Services.TryAddSingleton(postgresOptions);
        queueOptions.Services.TryAddScoped<IStorageService, PostgresStorageService>();
    }
}
