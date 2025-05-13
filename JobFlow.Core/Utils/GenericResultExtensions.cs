using System.Linq;
using FluentResults;
using JobFlow.Core.Abstractions;

namespace JobFlow.Core.Utils;

public static class GenericResultExtensions
{
    public static ResultDto ToResultDto<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new ResultDto { IsSuccess = true, Value = result.Value };

        return new ResultDto
        {
            IsSuccess = false,
            Errors = result.Errors.Select(x => x.Message).ToList(),
        };
    }
}
