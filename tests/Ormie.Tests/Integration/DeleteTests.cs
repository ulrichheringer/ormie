using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class DeleteTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task DeleteAsync_removes_existing_row()
    {
        var user = new User { Email = "alice@example.com", Name = "Alice" };
        await Orm.InsertAsync(user);

        await Orm.DeleteAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteAsync_throws_when_entity_is_null()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Orm.DeleteAsync<User>(null!));
    }

    [Fact]
    public async Task DeleteAsync_leaves_other_rows_unchanged()
    {
        var alice = new User { Email = "alice@example.com", Name = "Alice" };
        var bob = new User { Email = "bob@example.com", Name = "Bob" };
        await Orm.InsertAsync(alice);
        await Orm.InsertAsync(bob);

        await Orm.DeleteAsync(alice);

        var loadedBob = await Orm.GetByIdAsync<User>(bob.Id);

        Assert.NotNull(loadedBob);
        Assert.Equal("Bob", loadedBob.Name);
    }

    [Fact]
    public async Task DeleteAsync_does_not_throw_when_row_is_missing()
    {
        var user = new User { Id = 999, Email = "ghost@example.com", Name = "Ghost" };

        var act = () => Orm.DeleteAsync(user);

        await act();
    }

    [Fact]
    public async Task DeleteAsync_only_removes_matching_row()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var toDelete = new User { Id = 1, Email = "a@example.com", Name = "A" };
        await Orm.DeleteAsync(toDelete);

        var remaining = await Orm.QueryAsync<User>("SELECT * FROM users ORDER BY Id");

        Assert.Single(remaining);
        Assert.Equal("B", remaining[0].Name);
    }
}
