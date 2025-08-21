using System;
using System.Threading.Tasks;
using JobFlow.Postgres.Tests;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SliceFixture))]

namespace JobFlow.Postgres.Tests;

public class SliceFixture : IAsyncLifetime
{
    private IHost? _app;
    private readonly PostgreSqlContainer _postgresSqlContainer;

    private string PostgresSqlConnectionString
    {
        get
        {
            var port = _postgresSqlContainer.GetMappedPublicPort("5432");
            return $"User ID=postgres;Password=postgres;Host=localhost;Port={port};Database=postgres";
        }
    }

    public IHost App => _app!;

    public SliceFixture()
    {
        Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://localhost:2376");

        Environment.SetEnvironmentVariable(
            "TESTCONTAINERS_RYUK_CONTAINER_IMAGE",
            "testcontainers/ryuk:latest"
        );

        _postgresSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("postgres")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _postgresSqlContainer.StartAsync();

        _app = await TestHost.CreateAsync(PostgresSqlConnectionString);

        Environment.SetEnvironmentVariable(
            "ConnectionStrings:Postgres",
            PostgresSqlConnectionString
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _app!.StopAsync();
        await _postgresSqlContainer.DisposeAsync();
    }
}
