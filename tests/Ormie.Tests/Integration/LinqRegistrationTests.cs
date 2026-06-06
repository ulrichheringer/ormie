using Ormie.Linq;
using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class LinqRegistrationTests : OrmieIntegrationTestBase
{
    protected override Task ConfigureAsync(global::Ormie.Ormie orm) => Task.CompletedTask;

    [Fact]
    public async Task Query_throws_when_entity_not_registered()
    {
        var act = () => Orm.Query<User>().ToListAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Contains("not registered", ex.Message);
    }
}
