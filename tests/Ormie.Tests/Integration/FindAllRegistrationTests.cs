using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class FindAllRegistrationTests : OrmieIntegrationTestBase
{
    protected override Task ConfigureAsync(global::Ormie.Ormie orm) => Task.CompletedTask;

    [Fact]
    public async Task FindAllAsync_throws_when_entity_not_registered()
    {
        var act = () => Orm.FindAllAsync<User>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("not registered", ex.Message);
    }
}
