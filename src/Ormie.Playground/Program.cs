using Ormie;
using Ormie.Playground.Models;

var dbPath = Path.Combine(AppContext.BaseDirectory, "playground.db");
var connectionString = $"Data Source={dbPath}";

await using var orm = new Ormie.Ormie(connectionString);
orm.Register<User>();
await orm.MigrateAsync<User>();

await orm.ExecuteAsync("DELETE FROM users");

var alice = new User { Email = "alice@example.com", Name = "Alice" };
await orm.InsertAsync(alice);

Console.WriteLine($"Inserted user #{alice.Id}: {alice.Name} <{alice.Email}>");

var loaded = await orm.GetByIdAsync<User>(alice.Id);
Console.WriteLine($"Loaded back: {loaded?.Name}");

var allUsers = await orm.QueryAsync<User>("SELECT * FROM users ORDER BY Id");
Console.WriteLine($"Total users: {allUsers.Count}");
