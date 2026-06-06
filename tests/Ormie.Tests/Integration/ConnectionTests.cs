using Ormie.Tests.Entities;

namespace Ormie.Tests.Integration;

public class ConnectionTests
{
    [Fact]
    public async Task Ormie_opens_connection_from_connection_string()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ormie-{Guid.NewGuid():N}.db");

        try
        {
            await using var orm = new global::Ormie.Ormie($"Data Source={dbPath}");
            orm.Register<User>();
            await orm.MigrateAsync<User>();
            await orm.InsertAsync(new User { Email = "file@example.com", Name = "File" });

            var loaded = await orm.GetByIdAsync<User>(1);

            Assert.NotNull(loaded);
            Assert.Equal("file@example.com", loaded.Email);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
