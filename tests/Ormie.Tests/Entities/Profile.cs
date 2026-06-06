using Ormie.Mapping;

namespace Ormie.Tests.Entities;

[Table("profiles")]
public class Profile
{
    [Key]
    public int Id { get; set; }

    public string? Bio { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public int? Score { get; set; }
}
