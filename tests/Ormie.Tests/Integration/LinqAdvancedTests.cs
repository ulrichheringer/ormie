using Ormie.Tests.Entities;
using Ormie.Tests.Support;

namespace Ormie.Tests.Integration;

public class LinqAdvancedTests : OrmieIntegrationTestBase
{
    protected override async Task ConfigureAsync(global::Ormie.Ormie orm)
    {
        orm.Register<User>();
        orm.Register<Post>();
        await orm.MigrateAsync<User>();
        await orm.MigrateAsync<Post>();
    }

    [Fact]
    public async Task Select_projects_columns_to_dto()
    {
        await Orm.InsertAsync(new User { Email = "alice@example.com", Name = "Alice" });

        var rows = await Orm.Query<User>()
            .Where(user => user.Name == "Alice")
            .Select(user => new UserNameEmailDto
            {
                Name = user.Name,
                Email = user.Email
            })
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].Name);
        Assert.Equal("alice@example.com", rows[0].Email);
    }

    [Fact]
    public async Task Join_selects_from_both_entities()
    {
        var user = new User { Email = "alice@example.com", Name = "Alice" };
        await Orm.InsertAsync(user);
        await Orm.InsertAsync(new Post { UserId = user.Id, Title = "Hello" });

        var rows = await Orm.Query<User>()
            .Join<Post>((user, post) => user.Id == post.UserId)
            .Select((user, post) => new UserPostSummary
            {
                UserName = user.Name,
                PostTitle = post.Title
            })
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].UserName);
        Assert.Equal("Hello", rows[0].PostTitle);
    }

    [Fact]
    public async Task Join_where_filters_joined_rows()
    {
        var alice = new User { Email = "alice@example.com", Name = "Alice" };
        var bob = new User { Email = "bob@example.com", Name = "Bob" };
        await Orm.InsertAsync(alice);
        await Orm.InsertAsync(bob);
        await Orm.InsertAsync(new Post { UserId = alice.Id, Title = "Alice Post" });
        await Orm.InsertAsync(new Post { UserId = bob.Id, Title = "Bob Post" });

        var rows = await Orm.Query<User>()
            .Join<Post>((user, post) => user.Id == post.UserId)
            .Where((user, post) => user.Name == "Alice")
            .Select((user, post) => new UserPostSummary
            {
                UserName = user.Name,
                PostTitle = post.Title
            })
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal("Alice Post", rows[0].PostTitle);
    }

    [Fact]
    public async Task GroupBy_returns_counts_by_key()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "Alice" });
        await Orm.InsertAsync(new User { Email = "c@example.com", Name = "Bob" });

        var groups = await Orm.Query<User>()
            .GroupBy(user => user.Name)
            .ToCountListAsync();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, group => group.Key == "Alice" && group.Count == 2);
        Assert.Contains(groups, group => group.Key == "Bob" && group.Count == 1);
    }

    [Fact]
    public async Task Where_supports_to_lower_comparison()
    {
        await Orm.InsertAsync(new User { Email = "alice@example.com", Name = "Alice" });

        var users = await Orm.Query<User>()
            .Where(user => user.Name.ToLower() == "alice")
            .ToListAsync();

        Assert.Single(users);
    }

    [Fact]
    public async Task Where_supports_is_null_or_empty()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "Bob" });

        var users = await Orm.Query<User>()
            .Where(user => string.IsNullOrEmpty(user.Name))
            .ToListAsync();

        Assert.Single(users);
    }

    [Fact]
    public async Task MaxAsync_returns_max_value()
    {
        await Orm.InsertAsync(new User { Email = "a@example.com", Name = "A" });
        await Orm.InsertAsync(new User { Email = "b@example.com", Name = "C" });

        var maxId = await Orm.Query<User>().MaxAsync(user => user.Id);

        Assert.Equal(2, maxId);
    }

    [Fact]
    public async Task SingleAsync_returns_single_row()
    {
        await Orm.InsertAsync(new User { Email = "solo@example.com", Name = "Solo" });

        var user = await Orm.Query<User>()
            .Where(u => u.Name == "Solo")
            .SingleAsync();

        Assert.Equal("solo@example.com", user.Email);
    }

    [Fact]
    public async Task SingleAsync_throws_when_no_match()
    {
        var act = () => Orm.Query<User>().Where(u => u.Name == "Missing").SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }
}
