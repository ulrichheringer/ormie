# Ormie

A minimal object–relational mapper for .NET 10 and SQLite. Maps C# classes to tables via attributes, generates SQL from LINQ expressions, and materializes rows back into objects.

**Target:** learning and experimentation. Not production-ready.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQLite (via `Microsoft.Data.Sqlite`)

## Quick start

```bash
git clone git@github.com:ulrichheringer/ormie.git
cd ormie
dotnet test
dotnet run --project src/Ormie.Playground
```

```csharp
await using var orm = new Ormie.Ormie("Data Source=app.db");

orm.Register<User>();
await orm.MigrateAsync<User>();

var user = new User { Email = "alice@example.com", Name = "Alice" };
await orm.InsertAsync(user);

var loaded = await orm.GetByIdAsync<User>(user.Id);
```

## Entity mapping

Declare the database shape on a plain C# class:

```csharp
using Ormie.Mapping;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    [Column("email")]           // explicit column name
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;  // → "name" (snake_case)
}
```

| Attribute | Purpose |
|---|---|
| `[Table("name")]` | Table name (defaults to class name) |
| `[Key]` | Primary key. `int`/`long` keys auto-increment on insert |
| `[Column("name")]` | Column name (defaults to snake_case property name) |
| `[NotMapped]` | Exclude property from persistence |
| `[ForeignKey(nameof(Parent))]` | Marks FK column; navigation props are ignored automatically |

Supported property types: `int`, `long`, `string`, `bool`, `double`, `DateTime`, `DateTimeOffset`, `Guid`, and nullable variants.

## Session API

### Register and migrate

```csharp
orm.Register<User>();
orm.Register<Post>();
await orm.MigrateAsync<User>();
await orm.MigrateAsync<Post>();
```

`MigrateAsync` runs `CREATE TABLE IF NOT EXISTS`, then adds any missing columns with `ALTER TABLE … ADD COLUMN`. Existing rows keep their data; new columns are `NULL`. Column renames and type changes are not supported.

### CRUD

```csharp
await orm.InsertAsync(user);                  // sets auto-increment Id on entity
var user = await orm.GetByIdAsync<User>(1);
var all  = await orm.FindAllAsync<User>();
await orm.UpdateAsync(user);
await orm.DeleteAsync(user);
```

### Raw SQL

```csharp
var rows = await orm.QueryAsync<User>("SELECT * FROM users WHERE name = @Name", new { Name = "Alice" });
await orm.ExecuteAsync("DELETE FROM users WHERE id = @id", new { id = 1 });
```

## LINQ queries

Start from `Query<T>()` and chain filters. Expressions are translated to parameterized SQL.

### Filter, sort, page

```csharp
var users = await orm.Query<User>()
    .Where(u => u.Email.Contains("example"))
    .Where(u => u.Name.ToLower() == "alice")
    .OrderBy(u => u.Name)
    .Skip(10)
    .Take(20)
    .ToListAsync();

var count = await orm.Query<User>().Where(u => u.Name == "Alice").CountAsync();
var any   = await orm.Query<User>().Where(u => u.Name == "Alice").AnyAsync();
var one   = await orm.Query<User>().Where(u => u.Name == "Alice").SingleAsync();
var maxId = await orm.Query<User>().MaxAsync(u => u.Id);
```

Supported expression operators: `==`, `!=`, `<`, `>`, `<=`, `>=`, `&&`, `||`, `!`, `string.Contains`, `StartsWith`, `EndsWith`, `ToLower`, `ToUpper`, `string.IsNullOrEmpty`, and basic arithmetic.

### Select (projection)

```csharp
var dtos = await orm.Query<User>()
    .Select(u => new UserDto { Name = u.Name, Email = u.Email })
    .ToListAsync();
```

### Join

```csharp
var rows = await orm.Query<User>()
    .Join<Post>((u, p) => u.Id == p.UserId)
    .Where((u, p) => u.Name == "Alice")
    .Select((u, p) => new { u.Name, p.Title })
    .ToListAsync();
```

Only `INNER JOIN` is supported.

### GroupBy

```csharp
var groups = await orm.Query<User>()
    .GroupBy(u => u.Name)
    .ToCountListAsync();   // → List<GroupCount<string>> with Key and Count
```

## Transactions

```csharp
await using var tx = await orm.BeginTransactionAsync();
await orm.InsertAsync(user);
await orm.InsertAsync(post);
await tx.CommitAsync();    // omit Commit → rollback on dispose

// or scoped:
await orm.TransactionAsync(async db =>
{
    await db.InsertAsync(user);
    await db.InsertAsync(post);
});
```

## Project layout

```
src/Ormie/              Core library
src/Ormie.Playground/   Console demo (CRUD + LINQ examples)
tests/Ormie.Tests/      xUnit tests (87+, ~96% line coverage)
```

## Development

```bash
dotnet build
dotnet test
dotnet watch --project tests/Ormie.Tests test   # TDD loop
```

CI runs on push/PR to `dev` and `master`. Tests must pass with ≥ 80% line coverage.

## Branch workflow

```
feat/* → PR → dev → PR → master
```

## Version

Current release: **v0.2** — CRUD, nullable types, snake_case mapping, transactions, LINQ (Where/Order/Select/Join/GroupBy), schema column migration.
