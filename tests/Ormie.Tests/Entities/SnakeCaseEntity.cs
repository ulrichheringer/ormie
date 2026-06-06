using Ormie.Mapping;

namespace Ormie.Tests.Entities;

public class SnakeCaseEntity
{
    [Key]
    public int UserId { get; set; }

    public string EmailAddress { get; set; } = string.Empty;

    public string HTTPStatus { get; set; } = string.Empty;
}
