using System.Threading.Tasks;
using JobFlow.Core;
using JobFlow.Core.Abstractions;
using JobFlow.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Tests.Postgres;

public class RestartJobTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        var jobFlowOptions = scope.ServiceProvider.GetRequiredService<JobFlowOptions>();

        var pollingInterval = jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value;

        var jobId = await jobQueue.SubmitJobAsync(
            new SimpleJob { Delay = 10 * pollingInterval },
            cancellationToken: TestContext.Current.CancellationToken
        );

        await Task.Delay(
            20 * jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value,
            TestContext.Current.CancellationToken
        );

        await jobQueue.RestartJobAsync(jobId, TestContext.Current.CancellationToken);

        await Task.Delay(
            40 * jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value,
            TestContext.Current.CancellationToken
        );

        var job = await jobQueue.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        job!.Status.ShouldBe(JobStatus.Completed);
        job.Data.ShouldBeNull();
    }
}
