using System.Threading.Tasks;
using JobFlow.Core;
using JobFlow.Core.Abstractions;
using JobFlow.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Tests.Postgres;

public class JobStoppingTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        var jobFlowOptions = scope.ServiceProvider.GetRequiredService<JobFlowOptions>();

        var jobId = await jobQueue.SubmitJobAsync(
            new ToBeStoppedJob(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await Task.Delay(
            10 * jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value,
            TestContext.Current.CancellationToken
        );

        await jobQueue.StopJobAsync(jobId, TestContext.Current.CancellationToken);

        await Task.Delay(
            10 * jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value,
            TestContext.Current.CancellationToken
        );

        var job = await jobQueue.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        job!.Status.ShouldBe(JobStatus.Stopped);
        job.Data.ShouldBeNull();
    }
}
