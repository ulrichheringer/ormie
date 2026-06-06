using Microsoft.Data.Sqlite;

namespace Ormie.Tests.Support;

public abstract class OrmieIntegrationTestBase : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    protected global::Ormie.Ormie Orm { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        Orm = new global::Ormie.Ormie(_connection);
        await ConfigureAsync(Orm);
    }

    protected abstract Task ConfigureAsync(global::Ormie.Ormie orm);

    public async Task DisposeAsync()
    {
        await Orm.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
