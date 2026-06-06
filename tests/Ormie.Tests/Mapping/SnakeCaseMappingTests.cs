using Ormie.Mapping;
using Ormie.Tests.Entities;

namespace Ormie.Tests.Mapping;

public class SnakeCaseMappingTests
{
    [Theory]
    [InlineData(nameof(SnakeCaseEntity.UserId), "user_id")]
    [InlineData(nameof(SnakeCaseEntity.EmailAddress), "email_address")]
    [InlineData(nameof(SnakeCaseEntity.HTTPStatus), "http_status")]
    public void Map_converts_property_names_to_snake_case(string propertyName, string expectedColumnName)
    {
        var map = EntityMapper.Map<SnakeCaseEntity>();
        var property = map.Properties.Single(p => p.Property.Name == propertyName);

        Assert.Equal(expectedColumnName, property.ColumnName);
    }

    [Fact]
    public void Map_prefers_column_attribute_over_snake_case()
    {
        var map = EntityMapper.Map<User>();
        var email = map.Properties.Single(p => p.Property.Name == nameof(User.Email));

        Assert.Equal("email", email.ColumnName);
    }
}
