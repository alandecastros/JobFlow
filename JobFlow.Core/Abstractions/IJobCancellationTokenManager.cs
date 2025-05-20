using System.Threading;

namespace JobFlow.Core.Abstractions;

public interface IJobCancellationTokenManager
{
    /// <summary>
    /// Creates and registers a new CancellationTokenSource for a given job ID.
    /// </summary>
    /// <param name="jobId">The unique identifier for the job.</param>
    /// <returns>The CancellationToken associated with the created CancellationTokenSource.
    /// Returns CancellationToken.None if a CTS for this job ID already exists.</returns>
    CancellationToken RegisterJobTokenSource(string jobId);

    /// <summary>
    /// Requests cancellation for a specific job.
    /// </summary>
    /// <param name="jobId">The unique identifier for the job.</param>
    /// <returns>True if the CancellationTokenSource was found and cancellation was requested; otherwise, false.</returns>
    bool RequestJobCancellation(string jobId);

    /// <summary>
    /// Removes and disposes the CancellationTokenSource associated with the given job ID.
    /// </summary>
    /// <param name="jobId">The unique identifier for the job.</param>
    void UnregisterJobTokenSource(string jobId);

    /// <summary>
    /// Cancels all registered job token sources.
    /// Useful for application shutdown scenarios.
    /// </summary>
    void CancelAllJobTokenSources();
}
