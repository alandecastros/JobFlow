using System;
using System.Text.Json;

namespace JobFlow.Core.Utils;

public static class JsonSerializerUtils
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, JsonSerializerOptions);
    }

    public static JsonDocument SerializeToDocument(object json)
    {
        return JsonSerializer.SerializeToDocument(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            }
        );
    }

    public static object? Deserialize(string payload, Type type)
    {
        return JsonSerializer.Deserialize(payload, type, JsonSerializerOptions);
    }
}
