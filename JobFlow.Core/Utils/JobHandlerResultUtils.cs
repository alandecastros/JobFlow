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
