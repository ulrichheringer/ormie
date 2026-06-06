using System.Reflection;

namespace Ormie.Mapping;

public sealed class PropertyMap
{
    public required PropertyInfo Property { get; init; }
    public required string ColumnName { get; init; }
    public required string SqlType { get; init; }
    public bool IsKey { get; init; }
    public bool IsAutoIncrement { get; init; }
}
