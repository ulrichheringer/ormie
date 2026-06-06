using Ormie.Mapping;

namespace Ormie.Playground.Models;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
