using System.Reflection;
using System.Text;

namespace Ormie.Mapping;

public static class EntityMapper
{
    public static EntityMap Map<T>() => Map(typeof(T));

    public static EntityMap Map(Type clrType)
    {
        var tableName = clrType.GetCustomAttribute<TableAttribute>()?.Name ?? clrType.Name;
        var properties = clrType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(MapProperty)
            .ToList();

        return new EntityMap
        {
            ClrType = clrType,
            TableName = tableName,
            Properties = properties,
            Key = properties.FirstOrDefault(p => p.IsKey)
        };
    }

    private static PropertyMap MapProperty(PropertyInfo property)
    {
        var columnName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? ToSnakeCase(property.Name);
        var isKey = property.GetCustomAttribute<KeyAttribute>() is not null
            || string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase);
        var sqlType = ResolveSqlType(property.PropertyType);
        var isAutoIncrement = isKey && IsIntegerType(property.PropertyType);

        return new PropertyMap
        {
            Property = property,
            ColumnName = columnName,
            SqlType = sqlType,
            IsKey = isKey,
            IsAutoIncrement = isAutoIncrement
        };
    }

    private static string ResolveSqlType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(bool))
        {
            return "INTEGER";
        }

        if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
        {
            return "REAL";
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(Guid))
        {
            return "TEXT";
        }

        return "TEXT";
    }

    private static bool IsIntegerType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(int) || underlying == typeof(long);
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 5);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var needsUnderscore = i > 0
                    && (char.IsLower(name[i - 1])
                        || (i + 1 < name.Length && char.IsLower(name[i + 1])));

                if (needsUnderscore)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
