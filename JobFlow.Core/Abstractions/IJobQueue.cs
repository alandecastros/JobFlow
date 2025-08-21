using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobQueue
{
    Task<string> SubmitJobAsync<T>(T request, string? queue = null, CancellationToken ct = default)
        where T : notnull;
    Task<bool> StopJobAsync(string jobId, CancellationToken ct = default);
    Task<bool> RestartJobAsync(string jobId, CancellationToken ct = default);
    Task<Job?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task<Job<T?>?> GetJobAsync<T>(string jobId, CancellationToken ct = default);
    Task SetJobDataAsync(string jobId, object? data, CancellationToken ct = default);
}
