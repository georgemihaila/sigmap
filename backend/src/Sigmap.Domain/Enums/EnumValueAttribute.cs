namespace Sigmap.Domain.Enums;

[AttributeUsage(AttributeTargets.Field)]
public sealed class EnumValueAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}
