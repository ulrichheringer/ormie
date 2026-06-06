using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class QueryTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task GetByIdAsync_returns_inserted_row()
    {
        var user = new User { Email = "bob@example.com", Name = "Bob" };
        await Orm.InsertAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal("bob@example.com", loaded.Email);
        Assert.Equal("Bob", loaded.Name);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_row_missing()
    {
        var loaded = await Orm.GetByIdAsync<User>(999);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task QueryAsync_maps_rows_to_entities()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var users = await Orm.QueryAsync<User>(
            "SELECT * FROM users WHERE email LIKE @pattern",
            new { pattern = "%@example.com" });

        Assert.Equal(2, users.Count);
        Assert.Contains(users, u => u.Name == "A");
        Assert.Contains(users, u => u.Name == "B");
    }

    [Fact]
    public async Task GetByIdAsync_skips_null_columns_when_materializing()
    {
        await Orm.ExecuteAsync(
            "INSERT INTO users (email, name) VALUES (@email, NULL)",
            new { email = "null-name@example.com" });

        var loaded = await Orm.GetByIdAsync<User>(1);

        Assert.NotNull(loaded);
        Assert.Equal(string.Empty, loaded.Name);
    }
}
