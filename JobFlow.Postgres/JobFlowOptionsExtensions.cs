using System;
using JobFlow.Core;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace JobFlow.Postgres;

public static class JobFlowOptionsExtensions
{
    public static void UsePostgres(this JobFlowOptions options, PostgresOptions postgresOptions)
    {
        options.Services.TryAddKeyedSingleton<NpgsqlDataSource>(
            "postgres-job-queue",
            (_, _) =>
            {
                try
                {
                    var dataSource = NpgsqlDataSource.Create(postgresOptions.ConnectionString!);
                    using var connection = dataSource.OpenConnection();
                    var commandText = $"""
                    CREATE TABLE IF NOT EXISTS {postgresOptions.TableName} (
                    	id uuid NOT NULL,
                    	status int4 NOT NULL,
                    	queue varchar NOT NULL,
                    	worker_id varchar NULL,
                    	data varchar NULL,
                    	exception_message varchar NULL,
                    	exception_stacktrace varchar NULL,
                    	payload varchar NOT NULL,
                    	payload_type varchar NOT NULL,
                    	created_at timestamptz NOT NULL,
                    	updated_at timestamptz NOT NULL,
                    	CONSTRAINT {postgresOptions.TableName}_pk PRIMARY KEY (id)
                    );
                    CREATE INDEX IF NOT EXISTS {postgresOptions.TableName}_queue_status_idx ON {postgresOptions.TableName} USING btree (status, queue);
                    CREATE INDEX IF NOT EXISTS {postgresOptions.TableName}_status_idx ON {postgresOptions.TableName} USING btree (status);
                    """;

                    using var command = new NpgsqlCommand(commandText, connection);
                    command.ExecuteNonQuery();

                    return dataSource;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        );

        options.Services.TryAddSingleton(postgresOptions);
        options.Services.TryAddScoped<IStorageService, PostgresStorageService>();
    }
}
