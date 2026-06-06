namespace Ormie.Mapping;

public sealed class EntityMap
{
    public required Type ClrType { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyList<PropertyMap> Properties { get; init; }
    public PropertyMap? Key { get; init; }
}
