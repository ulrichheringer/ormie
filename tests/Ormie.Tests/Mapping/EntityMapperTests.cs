using Ormie.Mapping;
using Ormie.Tests.Entities;

namespace Ormie.Tests.Mapping;

public class EntityMapperTests
{
    [Fact]
    public void Map_returns_entity_map_for_user()
    {
        var map = EntityMapper.Map<User>();

        Assert.Equal(typeof(User), map.ClrType);
        Assert.Equal("users", map.TableName);
        Assert.Equal(3, map.Properties.Count);
        Assert.Equal("Id", map.Key?.ColumnName);
    }

    [Fact]
    public void Map_uses_column_attribute()
    {
        var map = EntityMapper.Map<User>();

        var email = map.Properties.Single(p => p.Property.Name == nameof(User.Email));

        Assert.Equal("email", email.ColumnName);
    }

    [Fact]
    public void Map_defaults_table_name_to_type_name()
    {
        var map = EntityMapper.Map<Article>();

        Assert.Equal(nameof(Article), map.TableName);
    }

    [Fact]
    public void Map_detects_key_from_key_attribute()
    {
        var map = EntityMapper.Map<Article>();

        Assert.Equal("PostId", map.Key?.ColumnName);
        Assert.True(map.Key?.IsAutoIncrement);
    }

    [Theory]
    [InlineData(nameof(Article.PostId), "INTEGER", true)]
    [InlineData(nameof(Article.Published), "INTEGER", false)]
    [InlineData(nameof(Article.Score), "REAL", false)]
    [InlineData(nameof(Article.Title), "TEXT", false)]
    [InlineData(nameof(Article.CreatedAt), "TEXT", false)]
    public void Map_resolves_sql_types(string propertyName, string expectedSqlType, bool isAutoIncrement)
    {
        var map = EntityMapper.Map<Article>();
        var property = map.Properties.Single(p => p.Property.Name == propertyName);

        Assert.Equal(expectedSqlType, property.SqlType);
        Assert.Equal(isAutoIncrement, property.IsAutoIncrement);
    }

    [Fact]
    public void Map_ignores_readonly_properties()
    {
        var map = EntityMapper.Map<ReadOnlyEntity>();

        Assert.Equal(2, map.Properties.Count);
        Assert.DoesNotContain(map.Properties, p => p.Property.Name == nameof(ReadOnlyEntity.Slug));
    }
}
