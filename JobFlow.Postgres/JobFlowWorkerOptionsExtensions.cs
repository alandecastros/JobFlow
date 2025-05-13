using JobFlow.Core;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace JobFlow.Postgres;

public static class JobFlowWorkerOptionsExtensions
{
    public static void UsePostgres(
        this JobFlowWorkerOptions options,
        PostgresOptions postgresOptions
    )
    {
        options.Services.TryAddKeyedSingleton<NpgsqlDataSource>(
            "postgres-job-queue",
            (_, _) => NpgsqlDataSource.Create(postgresOptions.ConnectionString!)
        );

        options.Services.TryAddSingleton(postgresOptions);
        options.Services.TryAddScoped<IStorageService, PostgresStorageService>();
    }
}
