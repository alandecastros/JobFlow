using System;

namespace JobFlow.Core.Abstractions;

public class Job
{
    public string Id { get; set; }
    public required int Status { get; init; }
    public required string Queue { get; init; }
    public required string Payload { get; init; }
    public required string PayloadType { get; init; }
    public string? WorkerId { get; init; }
    public string? Results { get; set; }
    public string? StackTrace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StoppedAt { get; set; }
}
