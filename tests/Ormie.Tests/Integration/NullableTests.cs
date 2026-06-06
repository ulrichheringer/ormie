using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class NullableTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<Profile>();
        await orm.MigrateAsync<Profile>();
    }

    [Fact]
    public async Task InsertAsync_persists_null_nullable_properties()
    {
        var profile = new Profile { Bio = null, LastSeenAt = null, Score = null };
        await Orm.InsertAsync(profile);

        var loaded = await Orm.GetByIdAsync<Profile>(profile.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.Bio);
        Assert.Null(loaded.LastSeenAt);
        Assert.Null(loaded.Score);
    }

    [Fact]
    public async Task InsertAsync_persists_nullable_values()
    {
        var lastSeen = new DateTime(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);
        var profile = new Profile { Bio = "Builder", LastSeenAt = lastSeen, Score = 42 };
        await Orm.InsertAsync(profile);

        var loaded = await Orm.GetByIdAsync<Profile>(profile.Id);

        Assert.Equal("Builder", loaded!.Bio);
        Assert.Equal(lastSeen, loaded.LastSeenAt);
        Assert.Equal(42, loaded.Score);
    }

    [Fact]
    public async Task UpdateAsync_sets_nullable_property_to_null()
    {
        var profile = new Profile { Bio = "Before", Score = 10 };
        await Orm.InsertAsync(profile);

        profile.Bio = null;
        profile.Score = null;
        await Orm.UpdateAsync(profile);

        var loaded = await Orm.GetByIdAsync<Profile>(profile.Id);

        Assert.Null(loaded!.Bio);
        Assert.Null(loaded.Score);
    }

    [Fact]
    public async Task UpdateAsync_sets_nullable_property_from_null_to_value()
    {
        var profile = new Profile { Bio = null, Score = null };
        await Orm.InsertAsync(profile);

        profile.Bio = "After";
        profile.Score = 99;
        await Orm.UpdateAsync(profile);

        var loaded = await Orm.GetByIdAsync<Profile>(profile.Id);

        Assert.Equal("After", loaded!.Bio);
        Assert.Equal(99, loaded.Score);
    }
}
