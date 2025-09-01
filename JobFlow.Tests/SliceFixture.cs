using System;
using System.Threading.Tasks;
using JobFlow.Tests;
using Microsoft.Extensions.Hosting;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SliceFixture))]

namespace JobFlow.Tests;

public class SliceFixture : IAsyncLifetime
{
    private IHost? _app;
    private IHost? _mongoDbApp;
    private readonly PostgreSqlContainer _postgresSqlContainer;
    private readonly MongoDbContainer _mongoDbContainer;

    private string PostgresSqlConnectionString
    {
        get
        {
            var port = _postgresSqlContainer.GetMappedPublicPort("5432");
            return $"User ID=postgres;Password=postgres;Host=localhost;Port={port};Database=postgres";
        }
    }

    private string MongoDbConnectionString
    {
        get
        {
            var port = _mongoDbContainer.GetMappedPublicPort("27017");
            return $"mongodb://admin:admin@localhost:{port}";
        }
    }

    public IHost App => _app!;
    public IHost MongoDbApp => _mongoDbApp!;

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

        _mongoDbContainer = new MongoDbBuilder()
            .WithImage("mongo:8.0.13")
            .WithUsername("admin")
            .WithPassword("admin")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _postgresSqlContainer.StartAsync();
        await _mongoDbContainer.StartAsync();

        _app = await PostgresHost.CreateAsync(PostgresSqlConnectionString);

        _mongoDbApp = await MongoDbHost.CreateAsync(
            MongoDbConnectionString,
            "test",
            "testcollection"
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _app!.StopAsync();
        await _mongoDbApp!.StopAsync();
        await _postgresSqlContainer.DisposeAsync();
    }
}
