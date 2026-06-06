using Ormie.Mapping;

namespace Ormie.Tests.Entities;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
