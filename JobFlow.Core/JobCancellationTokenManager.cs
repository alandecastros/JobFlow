using System;
using System.Collections.Concurrent;
using System.Threading;
using JobFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobFlow.Core;

public class JobCancellationTokenManager(ILogger<JobCancellationTokenManager> logger)
    : IJobCancellationTokenManager,
        IDisposable
{
    private readonly ConcurrentDictionary<
        string,
        CancellationTokenSource
    > _jobCancellationTokenSources = new();

    public CancellationToken RegisterJobTokenSource(string jobId)
    {
        var cts = new CancellationTokenSource();
        if (_jobCancellationTokenSources.TryAdd(jobId, cts))
        {
            logger.LogDebug("Registered CancellationTokenSource for Job ID: {JobId}", jobId);
            return cts.Token;
        }

        // If adding failed, it means a CTS for this job ID already exists.
        // This shouldn't happen if UnregisterJobTokenSource is called correctly.
        // Dispose the newly created CTS and return None or throw an exception.
        logger.LogWarning(
            "CancellationTokenSource for Job ID: {JobId} already exists. Returning CancellationToken.None.",
            jobId
        );
        cts.Dispose(); // Dispose the one we just created but couldn't add
        // Optionally, retrieve and return the existing token if that's desired:
        // if (_jobCancellationTokenSources.TryGetValue(jobId, out var existingCts)) return existingCts.Token;
        return CancellationToken.None; // Or throw new InvalidOperationException($"Job ID {jobId} already has a registered token source.");
    }

    public bool RequestJobCancellation(string jobId)
    {
        if (_jobCancellationTokenSources.TryGetValue(jobId, out var cts))
        {
            logger.LogInformation("Requesting cancellation for Job ID: {JobId}", jobId);
            try
            {
                cts.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                logger.LogWarning(
                    "Attempted to cancel an already disposed CancellationTokenSource for Job ID: {JobId}",
                    jobId
                );
                // The CTS was already disposed, which can happen if the job finished and unregistered concurrently.
                // Consider it as if cancellation was "successful" in the sense that the operation is no longer active.
                return true;
            }
        }

        return false;
    }

    public void UnregisterJobTokenSource(string jobId)
    {
        if (_jobCancellationTokenSources.TryRemove(jobId, out var cts))
        {
            logger.LogDebug(
                "Unregistered and disposing CancellationTokenSource for Job ID: {JobId}",
                jobId
            );
            try
            {
                // It's good practice to cancel before disposing if it might not have been cancelled.
                // However, if the job completed successfully, it shouldn't be cancelled.
                // If it failed or was externally cancelled, it would already be cancelled.
                // cts.Cancel(); // Uncomment if you want to ensure cancellation before disposal
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                logger.LogWarning(
                    "Attempted to unregister an already disposed CancellationTokenSource for Job ID: {JobId}",
                    jobId
                );
            }
        }
        else
        {
            logger.LogWarning(
                "No CancellationTokenSource found for Job ID: {JobId} to unregister.",
                jobId
            );
        }
    }

    public void CancelAllJobTokenSources()
    {
        logger.LogInformation("Cancelling all registered job token sources.");
        foreach (var entry in _jobCancellationTokenSources)
        {
            try
            {
                entry.Value.Cancel();
            }
            catch (ObjectDisposedException)
            {
                logger.LogWarning(
                    "Attempted to cancel an already disposed CancellationTokenSource for Job ID: {JobId} during CancelAll.",
                    entry.Key
                );
            }
        }
    }

    public void Dispose()
    {
        logger.LogInformation(
            "Disposing JobCancellationTokenManager. Cancelling and disposing all token sources."
        );
        foreach (var entry in _jobCancellationTokenSources)
        {
            try
            {
                entry.Value.Cancel(); // Ensure cancellation
                entry.Value.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
        _jobCancellationTokenSources.Clear();
        GC.SuppressFinalize(this);
    }
}
