using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentResults;
using JobFlow.Core.Abstractions;

namespace JobFlow.Core.Utils;

public static class JobHandlerResultUtils
{
    public static ResultDto ToResultDto(object? result)
    {
        ResultDto resultSerialized;

        var resultType = result?.GetType();

        switch (result)
        {
            case null:
                resultSerialized = Result.Ok().ToResultDto();
                break;
            case Result fluentResultBase:
            {
                resultSerialized = fluentResultBase.ToResultDto();
                break;
            }
            case List<Result> fluentResultBase:
            {
                var resultsDtos = fluentResultBase.Select(x => x.ToResultDto()).ToList();
                resultSerialized = Result.Ok(resultsDtos).ToResultDto();
                break;
            }
            default:
            {
                if (
                    resultType!.IsGenericType
                    && resultType.GetGenericTypeDefinition() == typeof(Result<>)
                )
                {
                    var genericArgType = resultType.GetGenericArguments()[0];
                    var extensionMethod = typeof(GenericResultExtensions)
                        .GetMethod("ToResultDto", BindingFlags.Static | BindingFlags.Public)
                        ?.MakeGenericMethod(genericArgType);

                    resultSerialized = (extensionMethod!.Invoke(null, [result]) as ResultDto)!;
                }
                else if (
                    resultType.IsGenericType
                    && resultType.GetGenericTypeDefinition() == typeof(List<>)
                    && resultType.GetGenericArguments()[0].IsGenericType
                    && resultType.GetGenericArguments()[0].GetGenericTypeDefinition()
                        == typeof(Result<>)
                )
                {
                    var innerResultType = resultType.GetGenericArguments()[0];
                    var genericArgType = innerResultType.GetGenericArguments()[0];
                    // Handle the List<Result<T>> case as needed, e.g. convert each Result<T> to DTO and aggregate
                    var toDtoMethod = typeof(GenericResultExtensions)
                        .GetMethod("ToResultDto", BindingFlags.Static | BindingFlags.Public)
                        ?.MakeGenericMethod(genericArgType);

                    var list = ((System.Collections.IEnumerable)result)
                        .Cast<object>()
                        .Select(r => toDtoMethod!.Invoke(null, new[] { r }) as ResultDto)
                        .ToList();

                    resultSerialized = Result.Ok(list).ToResultDto();
                }
                else
                {
                    resultSerialized = Result.Ok(result).ToResultDto();
                }

                break;
            }
        }

        return resultSerialized;
    }

    public static JsonDocument? SerializeResultToDocument(object? result)
    {
        JsonDocument? resultSerialized;

        var resultType = result?.GetType();

        switch (result)
        {
            case null:
                resultSerialized = JsonSerializerUtils.SerializeToDocument(
                    Result.Ok().ToResultDto()
                );
                break;
            case Result fluentResultBase:
            {
                var resultDto = fluentResultBase.ToResultDto();

                resultSerialized = JsonSerializerUtils.SerializeToDocument(resultDto);
                break;
            }
            default:
            {
                if (
                    resultType!.IsGenericType
                    && resultType.GetGenericTypeDefinition() == typeof(Result<>)
                )
                {
                    var genericArgType = resultType.GetGenericArguments()[0];
                    var extensionMethod = typeof(GenericResultExtensions)
                        .GetMethod("ToResultDto", BindingFlags.Static | BindingFlags.Public)
                        ?.MakeGenericMethod(genericArgType);

                    var resultDto = extensionMethod!.Invoke(null, [result]) as ResultDto;

                    resultSerialized = JsonSerializerUtils.SerializeToDocument(resultDto!);
                }
                else
                {
                    resultSerialized = JsonSerializerUtils.SerializeToDocument(
                        Result.Ok(result).ToResultDto()
                    );
                }

                break;
            }
        }

        return resultSerialized;
    }
}
