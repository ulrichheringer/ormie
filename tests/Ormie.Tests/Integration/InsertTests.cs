using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class InsertTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task InsertAsync_assigns_generated_id()
    {
        var user = new User { Email = "alice@example.com", Name = "Alice" };

        await Orm.InsertAsync(user);

        Assert.True(user.Id > 0);
    }

    [Fact]
    public async Task InsertAsync_throws_when_entity_is_null()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Orm.InsertAsync<User>(null!));
    }

    [Fact]
    public async Task InsertAsync_persists_column_mapped_property()
    {
        var user = new User { Email = "mapped@example.com", Name = "Mapped" };
        await Orm.InsertAsync(user);

        var loaded = await Orm.GetByIdAsync<User>(user.Id);

        Assert.Equal("mapped@example.com", loaded!.Email);
    }
}
