using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class TransactionTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task TransactionAsync_commits_multiple_inserts()
    {
        await Orm.TransactionAsync(async orm =>
        {
            await orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
            await orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });
        });

        var users = await Orm.FindAllAsync<User>();

        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task TransactionAsync_rolls_back_when_action_fails()
    {
        var act = () => Orm.TransactionAsync(async orm =>
        {
            await orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
            throw new InvalidOperationException("boom");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        var users = await Orm.FindAllAsync<User>();

        Assert.Empty(users);
    }

    [Fact]
    public async Task BeginTransactionAsync_commit_persists_changes()
    {
        await using (var transaction = await Orm.BeginTransactionAsync())
        {
            await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
            await transaction.CommitAsync();
        }

        var users = await Orm.FindAllAsync<User>();

        Assert.Single(users);
    }

    [Fact]
    public async Task BeginTransactionAsync_rollback_discards_changes()
    {
        await using (var transaction = await Orm.BeginTransactionAsync())
        {
            await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
            await transaction.RollbackAsync();
        }

        var users = await Orm.FindAllAsync<User>();

        Assert.Empty(users);
    }

    [Fact]
    public async Task BeginTransactionAsync_disposes_without_commit_rolls_back()
    {
        {
            await using var transaction = await Orm.BeginTransactionAsync();
            await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        }

        var users = await Orm.FindAllAsync<User>();

        Assert.Empty(users);
    }

    [Fact]
    public async Task BeginTransactionAsync_throws_when_transaction_already_active()
    {
        await using var outer = await Orm.BeginTransactionAsync();

        var act = () => Orm.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task CommitAsync_throws_when_transaction_already_completed()
    {
        await using var transaction = await Orm.BeginTransactionAsync();
        await transaction.CommitAsync();

        var act = () => transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }
}
