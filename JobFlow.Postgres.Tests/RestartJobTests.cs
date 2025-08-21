using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Postgres.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Postgres.Tests;

public class RestartJobTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

        var jobId = await jobQueue.SubmitJobAsync(
            new SimpleJob { Delay = 5000 },
            ct: TestContext.Current.CancellationToken
        );

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        await jobQueue.RestartJobAsync(jobId, TestContext.Current.CancellationToken);

        await Task.Delay(10000, TestContext.Current.CancellationToken);

        var job = await jobQueue.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        job!.Status.ShouldBe(JobStatus.Completed);
        job.Data.ShouldBeNull();
    }
}
