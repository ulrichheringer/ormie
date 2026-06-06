using Microsoft.Data.Sqlite;

namespace Ormie;

public sealed class OrmieTransaction : IAsyncDisposable
{
    private readonly Ormie _orm;
    private readonly SqliteTransaction _transaction;
    private bool _completed;

    internal OrmieTransaction(Ormie orm, SqliteTransaction transaction)
    {
        _orm = orm;
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsurePending();

        _completed = true;
        return _transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsurePending();

        _completed = true;
        return _transaction.RollbackAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            await _transaction.RollbackAsync().ConfigureAwait(false);
        }

        await _transaction.DisposeAsync().ConfigureAwait(false);
        _orm.ClearActiveTransaction();
    }

    private void EnsurePending()
    {
        if (_completed)
        {
            throw new InvalidOperationException("This transaction has already been completed.");
        }
    }
}
