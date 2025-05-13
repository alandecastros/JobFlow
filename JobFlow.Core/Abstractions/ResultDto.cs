using System.Collections.Generic;

namespace JobFlow.Core.Abstractions;

public class ResultDto
{
    public bool IsSuccess { get; set; }
    public List<string> Errors { get; set; } = new();
    public object? Value { get; set; }
}
