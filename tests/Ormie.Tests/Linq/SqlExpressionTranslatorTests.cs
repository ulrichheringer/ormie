using System.Linq.Expressions;
using Ormie.Linq;
using Ormie.Mapping;
using Ormie.Tests.Entities;

namespace Ormie.Tests.Linq;

public class SqlExpressionTranslatorTests
{
    [Fact]
    public void Translate_maps_equality_to_sql()
    {
        var map = EntityMapper.Map<User>();
        var predicate = (Expression<Func<User, bool>>)(user => user.Name == "Alice");
        var translator = new SqlExpressionTranslator(map);

        var (sql, parameters) = translator.Translate(predicate.Body, predicate.Parameters[0]);

        Assert.Equal("(\"name\" = @p0)", sql);
        Assert.Equal("Alice", parameters["p0"]);
    }

    [Fact]
    public void Translate_maps_and_also_to_sql()
    {
        var map = EntityMapper.Map<User>();
        var predicate = (Expression<Func<User, bool>>)(user => user.Name == "Alice" && user.Id > 1);
        var translator = new SqlExpressionTranslator(map);

        var (sql, parameters) = translator.Translate(predicate.Body, predicate.Parameters[0]);

        Assert.Equal("((\"name\" = @p0) AND (\"id\" > @p1))", sql);
        Assert.Equal("Alice", parameters["p0"]);
        Assert.Equal(1, parameters["p1"]);
    }

    [Fact]
    public void Translate_maps_string_contains_to_like()
    {
        var map = EntityMapper.Map<User>();
        var predicate = (Expression<Func<User, bool>>)(user => user.Email.Contains("example"));
        var translator = new SqlExpressionTranslator(map);

        var (sql, parameters) = translator.Translate(predicate.Body, predicate.Parameters[0]);

        Assert.Equal("\"email\" LIKE @p0", sql);
        Assert.Equal("%example%", parameters["p0"]);
    }
}
