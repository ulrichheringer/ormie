using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class UpdateTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task UpdateAsync_changes_existing_row()
    {
        var user = new User { Email = "alice@example.com", Name = "Alice" };
        await Orm.InsertAsync(user);

        user.Name = "Bob";
        await Orm.UpdateAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Equal("Bob", loaded!.Name);
    }

    [Fact]
    public async Task UpdateAsync_throws_when_entity_is_null()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Orm.UpdateAsync<User>(null!));
    }

    [Fact]
    public async Task UpdateAsync_updates_column_mapped_property()
    {
        var user = new User { Email = "before@example.com", Name = "Alice" };
        await Orm.InsertAsync(user);

        user.Email = "after@example.com";
        await Orm.UpdateAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Equal("after@example.com", loaded!.Email);
    }

    [Fact]
    public async Task UpdateAsync_updates_multiple_properties()
    {
        var user = new User { Email = "before@example.com", Name = "Before" };
        await Orm.InsertAsync(user);

        user.Email = "after@example.com";
        user.Name = "After";
        await Orm.UpdateAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Equal("after@example.com", loaded!.Email);
        Assert.Equal("After", loaded.Name);
    }

    [Fact]
    public async Task UpdateAsync_leaves_other_rows_unchanged()
    {
        var alice = new User { Email = "alice@example.com", Name = "Alice" };
        var bob = new User { Email = "bob@example.com", Name = "Bob" };
        await Orm.InsertAsync(alice);
        await Orm.InsertAsync(bob);

        alice.Name = "Alice Updated";
        await Orm.UpdateAsync(alice);

        var loadedBob = await Orm.GetByIdAsync<User>(bob.Id);

        Assert.Equal("Bob", loadedBob!.Name);
        Assert.Equal("bob@example.com", loadedBob.Email);
    }

    [Fact]
    public async Task UpdateAsync_does_not_throw_when_row_is_missing()
    {
        var user = new User { Id = 999, Email = "ghost@example.com", Name = "Ghost" };

        var act = () => Orm.UpdateAsync(user);

        await act();
    }

    [Fact]
    public async Task UpdateAsync_does_not_create_row_when_id_is_missing()
    {
        var user = new User { Id = 999, Email = "ghost@example.com", Name = "Ghost" };
        await Orm.UpdateAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Null(loaded);
    }
}
