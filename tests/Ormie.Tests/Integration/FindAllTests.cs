using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class FindAllTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task FindAllAsync_returns_empty_list_when_table_is_empty()
    {
        var users = await Orm.FindAllAsync<User>();

        Assert.Empty(users);
    }

    [Fact]
    public async Task FindAllAsync_returns_all_rows()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var users = await Orm.FindAllAsync<User>();

        Assert.Equal(2, users.Count);
        Assert.Contains(users, u => u.Name == "A");
        Assert.Contains(users, u => u.Name == "B");
    }

    [Fact]
    public async Task FindAllAsync_maps_column_attributes()
    {
        await Orm.InsertAsync(new User { Email = "mapped@example.com", Name = "Mapped" });

        var users = await Orm.FindAllAsync<User>();

        Assert.Single(users);
        Assert.Equal("mapped@example.com", users[0].Email);
    }
}
