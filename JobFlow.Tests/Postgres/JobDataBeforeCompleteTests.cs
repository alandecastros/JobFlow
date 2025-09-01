using System.Threading.Tasks;
using JobFlow.Core;
using JobFlow.Core.Abstractions;
using JobFlow.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Tests.Postgres;

public class JobDataBeforeCompleteTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        var jobFlowOptions = scope.ServiceProvider.GetRequiredService<JobFlowOptions>();

        var jobId = await jobQueue.SubmitJobAsync(
            new ChangeDataJob(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await Task.Delay(
            5 * jobFlowOptions.Worker!.PollingIntervalInMilliseconds!.Value,
            TestContext.Current.CancellationToken
        );

        var job = await jobQueue.GetJobAsync<ProgressData>(
            jobId,
            TestContext.Current.CancellationToken
        );

        job!.Status.ShouldBe(JobStatus.Processing);
        job.Data!.Progress.ShouldBe(50);
    }
}
