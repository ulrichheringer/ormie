using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class RegistrationTests : OrmieIntegrationTestBase
{
    protected override Task ConfigureAsync(global::Ormie.Ormie orm) => Task.CompletedTask;

    [Fact]
    public async Task InsertAsync_throws_when_entity_not_registered()
    {
        var act = () => Orm.InsertAsync(new User { Email = "x@example.com", Name = "X" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public async Task GetByIdAsync_throws_when_entity_has_no_key()
    {
        Orm.Register<KeylessEntity>();
        await Orm.MigrateAsync<KeylessEntity>();

        var act = () => Orm.GetByIdAsync<KeylessEntity>(1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("no key", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_throws_when_entity_not_registered()
    {
        var act = () => Orm.UpdateAsync(new User { Email = "x@example.com", Name = "X" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_throws_when_entity_has_no_key()
    {
        Orm.Register<KeylessEntity>();
        await Orm.MigrateAsync<KeylessEntity>();

        var act = () => Orm.UpdateAsync(new KeylessEntity { Name = "X" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("no key", ex.Message);
    }
}
