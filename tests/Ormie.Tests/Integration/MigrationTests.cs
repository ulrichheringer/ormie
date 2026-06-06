using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class MigrationTests : OrmieIntegrationTestBase
{
    protected override Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<SchemaVersionEntity>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MigrateAsync_creates_table_on_first_run()
    {
        await Orm.MigrateAsync<SchemaVersionEntity>();
        await Orm.InsertAsync(new SchemaVersionEntity { Title = "Draft" });

        var loaded = await Orm.GetByIdAsync<SchemaVersionEntity>(1);

        Assert.Equal("Draft", loaded!.Title);
    }

    [Fact]
    public async Task MigrateAsync_adds_new_columns_to_existing_table()
    {
        await Orm.MigrateAsync<SchemaVersionEntity>();
        await Orm.InsertAsync(new SchemaVersionEntity { Title = "Draft" });

        Orm.Register<SchemaVersionEntityV2>();
        await Orm.MigrateAsync<SchemaVersionEntityV2>();

        var loaded = await Orm.GetByIdAsync<SchemaVersionEntityV2>(1);
        Assert.Equal("Draft", loaded!.Title);
        Assert.Null(loaded.Tag);

        await Orm.InsertAsync(new SchemaVersionEntityV2 { Title = "Published", Tag = "news" });
        var published = await Orm.GetByIdAsync<SchemaVersionEntityV2>(2);
        Assert.Equal("news", published!.Tag);
    }
}
