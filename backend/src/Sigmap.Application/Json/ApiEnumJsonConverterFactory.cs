using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigmap.Domain.Enums;

namespace Sigmap.Application.Json;

/// <summary>
/// Serializes domain enums to the string literals the frontend contract uses
/// (lowercase enum name, or an explicit <see cref="EnumValueAttribute"/> value).
/// </summary>
public sealed class ApiEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ApiEnumJsonConverter<>).MakeGenericType(typeToConvert))!;
}

public sealed class ApiEnumJsonConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return default;
        var s = reader.GetString();
        if (s is null) return default;
        return EnumStrings.Parse<T>(s);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(EnumStrings.ToString(value));
}
