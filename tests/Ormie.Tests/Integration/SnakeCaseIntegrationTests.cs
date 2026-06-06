using Ormie.Mapping;
using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class SnakeCaseIntegrationTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<SnakeCaseEntity>();
        await orm.MigrateAsync<SnakeCaseEntity>();
    }

    [Fact]
    public async Task InsertAsync_persists_entity_with_snake_case_columns()
    {
        var entity = new SnakeCaseEntity
        {
            UserId = 7,
            EmailAddress = "snake@example.com",
            HTTPStatus = "ok"
        };

        await Orm.InsertAsync(entity);

        var loaded = await Orm.GetByIdAsync<SnakeCaseEntity>(entity.UserId);

        Assert.Equal("snake@example.com", loaded!.EmailAddress);
        Assert.Equal("ok", loaded.HTTPStatus);
    }
}
