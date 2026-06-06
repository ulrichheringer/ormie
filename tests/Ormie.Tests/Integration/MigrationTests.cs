using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class MigrationTests : OrmieIntegrationTestBase
{
    protected override Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MigrateAsync_is_idempotent()
    {
        await Orm.MigrateAsync<User>();
        await Orm.MigrateAsync<User>();

        await Orm.InsertAsync(new User { Email = "ok@example.com", Name = "Ok" });

        var loaded = await Orm.GetByIdAsync<User>(1);

        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task ExecuteAsync_runs_parameterized_sql()
    {
        await Orm.MigrateAsync<User>();
        await Orm.ExecuteAsync(
            "INSERT INTO users (email, name) VALUES (@email, @name)",
            new { email = "exec@example.com", name = "Exec" });

        var users = await Orm.QueryAsync<User>("SELECT * FROM users WHERE email = @email", new { email = "exec@example.com" });

        Assert.Single(users);
        Assert.Equal("Exec", users[0].Name);
    }
}
