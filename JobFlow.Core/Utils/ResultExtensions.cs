using System.Linq;
using FluentResults;
using JobFlow.Core.Abstractions;

namespace JobFlow.Core.Utils;

public static class ResultExtensions
{
    public static ResultDto ToResultDto(this Result result)
    {
        if (result.IsSuccess)
            return new ResultDto { IsSuccess = true };

        return new ResultDto
        {
            IsSuccess = false,
            Errors = result.Errors.Select(x => x.Message).ToList(),
        };
    }
}
