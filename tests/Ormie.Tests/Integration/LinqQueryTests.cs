using Ormie.Linq;
using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class LinqQueryTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        await orm.MigrateAsync<User>();
    }

    [Fact]
    public async Task Where_ToListAsync_filters_by_string_equality()
    {
        await Orm.InsertAsync(new User { Email = "alice@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "bob@example.com", Name = "Bob" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name == "Alice")
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal("alice@example.com", users[0].Email);
    }

    [Fact]
    public async Task Where_ToListAsync_filters_by_int_comparison()
    {
        var first = new User { Email = "a@example.com", Name = "A" };
        var second = new User { Email = "b@example.com", Name = "B" };
        await Orm.InsertAsync(first);
        await Orm.InsertAsync(second);

        var users = await Orm.Query<User>()
            .Where(user => user.Id > first.Id)
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal("B", users[0].Name);
    }

    [Fact]
    public async Task Where_ToListAsync_supports_multiple_conditions()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "Bob" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name == "Alice" && user.Email.Contains("example"))
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal("Alice", users[0].Name);
    }

    [Fact]
    public async Task Where_ToListAsync_supports_or_conditions()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "Bob" });
        await Orm.InsertAsync(new User { Email = "c@example.com", Name = "Carol" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name == "Alice" || user.Name == "Bob")
            .ToListAsync();

        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task OrderBy_ToListAsync_sorts_results()
    {
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });

        var users = await Orm.Query<User>()
            .OrderBy(user => user.Name)
            .ToListAsync();

        Assert.Equal(["A", "B"], users.Select(user => user.Name));
    }

    [Fact]
    public async Task OrderByDescending_ToListAsync_sorts_results()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var users = await Orm.Query<User>()
            .OrderByDescending(user => user.Name)
            .ToListAsync();

        Assert.Equal(["B", "A"], users.Select(user => user.Name));
    }

    [Fact]
    public async Task Take_and_Skip_page_results()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });
        await Orm.InsertAsync(new User { Email = "c@example.com", Name = "C" });

        var users = await Orm.Query<User>()
            .OrderBy(user => user.Name)
            .Skip(1)
            .Take(1)
            .ToListAsync();

        Assert.Single(users);
        Assert.Equal("B", users[0].Name);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_returns_first_match()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var user = await Orm.Query<User>()
            .Where(u => u.Name == "A")
            .FirstOrDefaultAsync();

        Assert.NotNull(user);
        Assert.Equal("a@example.com", user.Email);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_returns_null_when_no_match()
    {
        var user = await Orm.Query<User>()
            .Where(u => u.Name == "Missing")
            .FirstOrDefaultAsync();

        Assert.Null(user);
    }

    [Fact]
    public async Task CountAsync_returns_matching_row_count()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "B" });

        var count = await Orm.Query<User>()
            .Where(user => user.Email.Contains("example"))
            .CountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AnyAsync_returns_false_when_no_match()
    {
        var any = await Orm.Query<User>()
            .Where(user => user.Name == "Missing")
            .AnyAsync();

        Assert.False(any);
    }

    [Fact]
    public async Task Where_uses_closure_values()
    {
        var targetName = "Alice";
        await Orm.InsertAsync(new User { Email = "alice@example.com", Name = "Alice" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name == targetName)
            .ToListAsync();

        Assert.Single(users);
    }

    [Fact]
    public async Task Chained_where_clauses_combine_with_and()
    {
        await Orm.InsertAsync(new User { Email = "alice@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "bob@example.com", Name = "Bob" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name == "Alice")
            .Where(user => user.Email.Contains("example"))
            .ToListAsync();

        Assert.Single(users);
    }
}
