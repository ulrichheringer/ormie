using Ormie.Mapping;

namespace Ormie.Tests.Entities;

[Table("schema_versions")]
public class SchemaVersionEntity
{
    [Key]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
}

[Table("schema_versions")]
public class SchemaVersionEntityV2
{
    [Key]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Tag { get; set; }
}
