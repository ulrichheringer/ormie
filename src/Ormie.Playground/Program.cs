using Ormie.Playground.Models;

var dbPath = Path.Combine(AppContext.BaseDirectory, "playground.db");
var connectionString = $"Data Source={dbPath}";

await using var orm = new Ormie.Ormie(connectionString);
orm.Register<User>();
orm.Register<Post>();
await orm.MigrateAsync<User>();
await orm.MigrateAsync<Post>();

await orm.ExecuteAsync("DELETE FROM posts");
await orm.ExecuteAsync("DELETE FROM users");

var alice = new User { Email = "alice@example.com", Name = "Alice" };
var bob = new User { Email = "bob@example.com", Name = "Bob" };
await orm.InsertAsync(alice);
await orm.InsertAsync(bob);

await orm.InsertAsync(new Post { UserId = alice.Id, Title = "First post" });
await orm.InsertAsync(new Post { UserId = alice.Id, Title = "Second post" });
await orm.InsertAsync(new Post { UserId = bob.Id, Title = "Bob post" });

Console.WriteLine("=== CRUD ===");
Console.WriteLine($"Inserted users: {alice.Id}, {bob.Id}");

var loaded = await orm.GetByIdAsync<User>(alice.Id);
Console.WriteLine($"GetById: {loaded!.Name} <{loaded.Email}>");

Console.WriteLine();
Console.WriteLine("=== LINQ: Where + OrderBy ===");
var filtered = await orm.Query<User>()
    .Where(user => user.Email.Contains("example"))
    .OrderBy(user => user.Name)
    .ToListAsync();
foreach (var user in filtered)
{
    Console.WriteLine($"  {user.Name} <{user.Email}>");
}

Console.WriteLine();
Console.WriteLine("=== LINQ: Select ===");
var summaries = await orm.Query<User>()
    .Where(user => user.Name == "Alice")
    .Select(user => new UserNameEmailDto
    {
        Name = user.Name,
        Email = user.Email
    })
    .ToListAsync();
foreach (var summary in summaries)
{
    Console.WriteLine($"  {summary.Name} -> {summary.Email}");
}

Console.WriteLine();
Console.WriteLine("=== LINQ: Join ===");
var joined = await orm.Query<User>()
    .Join<Post>((user, post) => user.Id == post.UserId)
    .Where((user, post) => user.Name == "Alice")
    .Select((user, post) => new UserPostSummary
    {
        UserName = user.Name,
        PostTitle = post.Title
    })
    .ToListAsync();
foreach (var row in joined)
{
    Console.WriteLine($"  {row.UserName}: {row.PostTitle}");
}

Console.WriteLine();
Console.WriteLine("=== LINQ: GroupBy ===");
var groups = await orm.Query<User>()
    .GroupBy(user => user.Name)
    .ToCountListAsync();
foreach (var group in groups.OrderBy(group => group.Key))
{
    Console.WriteLine($"  {group.Key}: {group.Count}");
}
