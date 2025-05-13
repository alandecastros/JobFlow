namespace JobFlow.Core.Abstractions;

/// <summary>
/// Defines constant values for job statuses.
/// </summary>
public static class JobStatus
{
    /// <summary>
    /// Job is awaiting processing.
    /// </summary>
    public const int Pending = 1;

    /// <summary>
    /// Job is currently being processed by a consumer.
    /// </summary>
    public const int Processing = 2;

    /// <summary>
    /// Job was processed successfully.
    /// </summary>
    public const int Completed = 3;

    /// <summary>
    /// Job processing failed.
    /// </summary>
    public const int Failed = 4;

    // Add other statuses here if needed (e.g., Retrying, Cancelled)
}
