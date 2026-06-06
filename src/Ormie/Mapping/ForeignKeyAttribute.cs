namespace Ormie.Mapping;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ForeignKeyAttribute(string navigationProperty) : Attribute
{
    public string NavigationProperty { get; } = navigationProperty;
}
