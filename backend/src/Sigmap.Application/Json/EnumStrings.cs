using System.Reflection;
using Sigmap.Domain.Enums;

namespace Sigmap.Application.Json;

/// <summary>Maps domain enums to the string literals the frontend contract uses.</summary>
public static class EnumStrings
{
    public static string ToString<T>(T value)
        where T : struct, Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<EnumValueAttribute>();
        return attr?.Value ?? value.ToString().ToLowerInvariant();
    }

    public static T Parse<T>(string s)
        where T : struct, Enum
    {
        foreach (var v in Enum.GetValues<T>())
            if (string.Equals(ToString(v), s, StringComparison.OrdinalIgnoreCase))
                return v;
        return Enum.TryParse<T>(s, ignoreCase: true, out var parsed) ? parsed : default;
    }
}
