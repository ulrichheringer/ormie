using System.Data;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Ormie.Mapping;

namespace Ormie;

public sealed class Ormie : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Dictionary<Type, EntityMap> _maps = new();

    public Ormie(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public Ormie(SqliteConnection connection)
    {
        _connection = connection;
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    public void Register<T>() => Register(typeof(T));

    public void Register(Type clrType)
    {
        _maps[clrType] = EntityMapper.Map(clrType);
    }

    public async Task MigrateAsync<T>(CancellationToken cancellationToken = default)
    {
        var map = GetMap(typeof(T));
        var sql = BuildCreateTableSql(map);
        await ExecuteAsync(sql, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = GetMap(typeof(T));
        var insertProperties = map.Properties
            .Where(p => !(p.IsAutoIncrement && IsDefaultKeyValue(p.Property.GetValue(entity))))
            .ToList();

        var columns = string.Join(", ", insertProperties.Select(p => QuoteIdentifier(p.ColumnName)));
        var parameters = string.Join(", ", insertProperties.Select(p => $"@{p.ColumnName}"));
        var sql = $"INSERT INTO {QuoteIdentifier(map.TableName)} ({columns}) VALUES ({parameters})";

        await using var command = CreateCommand(sql);
        foreach (var property in insertProperties)
        {
            AddParameter(command, property.ColumnName, property.Property.GetValue(entity));
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (map.Key?.IsAutoIncrement == true)
        {
            await using var idCommand = CreateCommand("SELECT last_insert_rowid()");
            var id = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            map.Key.Property.SetValue(entity, Convert.ChangeType(id, map.Key.Property.PropertyType));
        }
    }

    public async Task<T?> GetByIdAsync<T>(object id, CancellationToken cancellationToken = default)
    {
        var map = GetMap(typeof(T));
        if (map.Key is null)
        {
            throw new InvalidOperationException($"Entity {typeof(T).Name} has no key mapped.");
        }

        var columns = string.Join(", ", map.Properties.Select(p => QuoteIdentifier(p.ColumnName)));
        var sql = $"SELECT {columns} FROM {QuoteIdentifier(map.TableName)} WHERE {QuoteIdentifier(map.Key.ColumnName)} = @id LIMIT 1";

        await using var command = CreateCommand(sql);
        AddParameter(command, "id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return default;
        }

        return Materialize<T>(map, reader);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var map = GetMap(typeof(T));

        await using var command = CreateCommand(sql);
        BindParameters(command, parameters);

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(Materialize<T>(map, reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<T>> FindAllAsync<T>(CancellationToken cancellationToken = default)
    {
        var map = GetMap(typeof(T));
        var columns = string.Join(", ", map.Properties.Select(p => QuoteIdentifier(p.ColumnName)));
        var sql = $"SELECT {columns} FROM {QuoteIdentifier(map.TableName)}";

        return await QueryAsync<T>(sql, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(sql);
        BindParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = GetMap(typeof(T));
        if (map.Key is null)
        {
            throw new InvalidOperationException($"Entity {typeof(T).Name} has no key mapped.");
        }

        var updateProperties = map.Properties
            .Where(p => !p.IsKey)
            .ToList();

        var setClause = string.Join(", ", updateProperties.Select(p =>
            $"{QuoteIdentifier(p.ColumnName)} = @{p.ColumnName}"));
        var sql = $"UPDATE {QuoteIdentifier(map.TableName)} SET {setClause} WHERE {QuoteIdentifier(map.Key.ColumnName)} = @id";

        await using var command = CreateCommand(sql);
        foreach (var property in updateProperties)
        {
            AddParameter(command, property.ColumnName, property.Property.GetValue(entity));
        }

        AddParameter(command, "id", map.Key.Property.GetValue(entity));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var map = GetMap(typeof(T));
        if (map.Key is null)
        {
            throw new InvalidOperationException($"Entity {typeof(T).Name} has no key mapped.");
        }

        var sql = $"DELETE FROM {QuoteIdentifier(map.TableName)} WHERE {QuoteIdentifier(map.Key.ColumnName)} = @id";

        await using var command = CreateCommand(sql);
        AddParameter(command, "id", map.Key.Property.GetValue(entity));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _connection.Dispose();

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private EntityMap GetMap(Type clrType)
    {
        if (_maps.TryGetValue(clrType, out var map))
        {
            return map;
        }

        throw new InvalidOperationException($"Entity {clrType.Name} is not registered. Call Register<{clrType.Name}>() first.");
    }

    private static string BuildCreateTableSql(EntityMap map)
    {
        var columns = map.Properties.Select(p =>
        {
            var sql = new StringBuilder();
            sql.Append(QuoteIdentifier(p.ColumnName));
            sql.Append(' ');
            sql.Append(p.SqlType);

            if (p.IsKey)
            {
                sql.Append(" PRIMARY KEY");
                if (p.IsAutoIncrement)
                {
                    sql.Append(" AUTOINCREMENT");
                }
            }

            return sql.ToString();
        });

        return $"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(map.TableName)} ({string.Join(", ", columns)})";
    }

    private static T Materialize<T>(EntityMap map, SqliteDataReader reader)
    {
        var entity = Activator.CreateInstance<T>();

        foreach (var property in map.Properties)
        {
            var ordinal = reader.GetOrdinal(property.ColumnName);
            if (reader.IsDBNull(ordinal))
            {
                if (IsNullableProperty(property.Property))
                {
                    property.Property.SetValue(entity, null);
                }

                continue;
            }

            var value = reader.GetValue(ordinal);
            var targetType = Nullable.GetUnderlyingType(property.Property.PropertyType) ?? property.Property.PropertyType;
            property.Property.SetValue(entity, Convert.ChangeType(value, targetType));
        }

        return entity;
    }

    private SqliteCommand CreateCommand(string sql)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static void BindParameters(SqliteCommand command, object? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var property in parameters.GetType().GetProperties())
        {
            if (!property.CanRead)
            {
                continue;
            }

            AddParameter(command, property.Name, property.GetValue(parameters));
        }
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue($"@{name}", value ?? DBNull.Value);
    }

    private static bool IsNullableProperty(PropertyInfo property)
    {
        var nullability = new NullabilityInfoContext().Create(property);
        return nullability.WriteState is NullabilityState.Nullable;
    }

    private static bool IsDefaultKeyValue(object? value) =>
        value switch
        {
            null => true,
            int i => i == 0,
            long l => l == 0L,
            _ => false
        };

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
