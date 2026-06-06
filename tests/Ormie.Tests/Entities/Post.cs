using Ormie.Mapping;

namespace Ormie.Tests.Entities;

[Table("posts")]
public class Post
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    [NotMapped]
    public User? User { get; set; }
}
