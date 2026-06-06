namespace Ormie.Tests.Entities;

public class ReadOnlyEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug => Name.ToLowerInvariant();
}
