using Ormie.Mapping;

namespace Ormie.Tests.Entities;

public class Article
{
    [Key]
    public long PostId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool Published { get; set; }

    public double Score { get; set; }

    public DateTime CreatedAt { get; set; }
}
