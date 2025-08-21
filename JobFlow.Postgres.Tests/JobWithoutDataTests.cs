using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Postgres.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Postgres.Tests;

public class JobWithoutDataTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

        var jobId = await jobQueue.SubmitJobAsync(
            new SimpleJob(),
            ct: TestContext.Current.CancellationToken
        );

        await Task.Delay(5000, TestContext.Current.CancellationToken);

        var job = await jobQueue.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        job!.Status.ShouldBe(JobStatus.Completed);
        job.Data.ShouldBeNull();
    }
}
