using System.Threading;
using System.Threading.Tasks;

namespace JobFlow.Core.Abstractions;

public interface IJobQueue
{
    Task<string> SubmitJobAsync<T>(
        T request,
        string? queue = null,
        CancellationToken cancellationToken = default
    )
        where T : notnull;
    Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<bool> RestartJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<Job?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
    Task<Job<T?>?> GetJobAsync<T>(string jobId, CancellationToken cancellationToken = default);
    Task SetJobDataAsync(string jobId, object? data, CancellationToken cancellationToken = default);
}
