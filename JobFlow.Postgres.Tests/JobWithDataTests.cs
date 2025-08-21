using System;
using System.Threading.Tasks;
using JobFlow.Core.Abstractions;
using JobFlow.Core.Utils;
using JobFlow.Postgres.Tests.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace JobFlow.Postgres.Tests;

public class JobWithDataTests(SliceFixture fixture)
{
    [Fact]
    public async Task Ok()
    {
        var scope = fixture.App.Services.CreateScope();

        var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

        var myId = Guid.NewGuid().ToString();

        var jobId = await jobQueue.SubmitJobAsync(
            new MyRequestJob() { MyId = myId },
            ct: TestContext.Current.CancellationToken
        );

        await Task.Delay(5000, TestContext.Current.CancellationToken);

        var job = await jobQueue.GetJobAsync(jobId, TestContext.Current.CancellationToken);

        var data = JsonSerializerUtils.Deserialize<MyResponse>(job!.Data!);

        job.Status.ShouldBe(JobStatus.Completed);
        data!.MyId.ShouldBe(myId);
    }
}
