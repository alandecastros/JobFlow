# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JobFlow is a .NET job queue and processing library with pluggable storage backends. It provides a distributed job processing system with support for job scheduling, cancellation, retrying, and monitoring.

The solution consists of:
- **JobFlow.Core**: Core abstractions, job queue, worker services, and dependency injection setup
- **JobFlow.MongoDb**: MongoDB storage implementation
- **JobFlow.Postgres**: PostgreSQL storage implementation  
- **JobFlow.Tests**: Comprehensive test suite using XUnit with Testcontainers for database testing

## Development Commands

### Build and Test
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build JobFlow.Core/JobFlow.Core.csproj
dotnet build JobFlow.MongoDb/JobFlow.MongoDb.csproj
dotnet build JobFlow.Postgres/JobFlow.Postgres.csproj

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test JobFlow.Tests/JobFlow.Tests.csproj

# Pack NuGet packages
dotnet pack JobFlow.Core/JobFlow.Core.csproj --configuration Release -o ./packages
dotnet pack JobFlow.MongoDb/JobFlow.MongoDb.csproj --configuration Release -o ./packages
dotnet pack JobFlow.Postgres/JobFlow.Postgres.csproj --configuration Release -o ./packages
```

### Development Setup
The project uses .NET 9.0 with nullable reference types enabled and implicit usings disabled for explicit control over imports.

## Architecture

### Core Components

**Job Processing Pipeline:**
1. Jobs are submitted via `IJobQueue.SubmitJobAsync<T>()` 
2. `JobPollingBackgroundService` polls storage for pending jobs
3. `JobWorkerBackgroundService` instances process jobs using registered handlers
4. Job handlers implement `IJobHandler<T>` or `IJobHandler<T,TR>` interfaces

**Key Abstractions:**
- `Job` and `Job<T>`: Core job entities with status, payload, metadata, and optional typed data
- `IStorageService`: Pluggable storage interface for job persistence
- `IJobHandler<T>` / `IJobHandler<T,TR>`: Job processing interfaces (void or return value)
- `IJobQueue`: Public API for job submission, cancellation, and retrieval
- `JobStatus`: Constants for job states (Pending=1, Processing=2, Completed=3, Failed=4, Stopped=5)

**Worker System:**
- Multiple worker instances per queue via `JobFlowWorkerQueueOptions.NumberOfWorkers`
- Channel-based job distribution using `IChannelManager`
- Cancellation token support via `IJobCancellationTokenManager`
- Worker identification for job ownership and cleanup

### Configuration Pattern

JobFlow uses a fluent configuration API:

```csharp
services.AddJobFlow(options =>
{
    options.UseMongoDb(mongoOptions => { /* config */ });
    // or options.UsePostgres(pgOptions => { /* config */ });
    
    options.UseWorker(workerOptions =>
    {
        workerOptions.WorkerId = "worker-1";
        workerOptions.Assemblies = [Assembly.GetExecutingAssembly()];
        workerOptions.AddQueue("default", queueOptions =>
        {
            queueOptions.NumberOfWorkers = 5;
        });
    });
});
```

### Storage Implementations

Both MongoDB and PostgreSQL implementations provide the same `IStorageService` interface:
- Atomic job state transitions
- Worker-based job locking during processing
- Cleanup of orphaned jobs when workers shut down
- Efficient polling with pending/in-progress counts

### Testing Infrastructure

Tests use Testcontainers for real database testing:
- `MongoDbHost` and `PostgresHost` manage containerized databases
- `SliceFixture` provides test isolation per class
- Tests are organized by storage provider: `MongoDb/` and `Postgres/` directories
- Comprehensive coverage of job lifecycle scenarios

## Code Patterns

### Job Handler Registration
Job handlers are automatically discovered and registered from specified assemblies. The system supports both void handlers (`IJobHandler<T>`) and handlers that return data (`IJobHandler<T,TR>`).

### Job Data Management
Jobs can store serialized data that gets updated during processing. Use `IJobQueue.SetJobDataAsync()` to store intermediate results or progress information.

### Cancellation and Restart
Jobs can be stopped via `IJobQueue.StopJobAsync()` and restarted via `IJobQueue.RestartJobAsync()`. The restart process waits for job completion/stop before resetting to pending status.

### Error Handling
Failed jobs capture exception details in `ExceptionMessage`, `ExceptionStacktrace`, and `ExceptionInnerStacktrace` properties for debugging.