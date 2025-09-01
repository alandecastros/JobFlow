using System;

namespace JobFlow.Core.Abstractions;

public class Job
{
    public string Id { get; set; } = null!;
    public required int Status { get; init; }
    public required string Queue { get; init; }
    public required string Payload { get; init; }
    public required string PayloadType { get; init; }
    public string? WorkerId { get; init; }
    public string? Data { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStacktrace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Job<T>
{
    public string Id { get; set; } = null!;
    public required int Status { get; init; }
    public required string Queue { get; init; }
    public required string Payload { get; init; }
    public required string PayloadType { get; init; }
    public string? WorkerId { get; init; }
    public T? Data { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStacktrace { get; set; }
    public string? ExceptionInnerStacktrace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
