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

    public static object? Deserialize(string payload, Type type)
    {
        return JsonSerializer.Deserialize(payload, type, JsonSerializerOptions);
    }

    public static T? Deserialize<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, JsonSerializerOptions);
    }
}
