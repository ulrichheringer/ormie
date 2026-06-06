using System.Linq.Expressions;
using System.Text;
using Ormie.Mapping;

namespace Ormie.Linq;

internal sealed record EntityAlias(EntityMap Map, string Alias, ParameterExpression Parameter);

internal sealed class SqlBuilder
{
    private int _parameterIndex;

    public StringBuilder Sql { get; } = new();

    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.Ordinal);

    public string AddParameter(object? value)
    {
        var name = $"p{_parameterIndex++}";
        Parameters[name] = value;
        return $"@{name}";
    }

    public static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}

internal static class SqlFormatting
{
    public static string QualifyColumn(string alias, string columnName) =>
        string.IsNullOrEmpty(alias)
            ? SqlBuilder.QuoteIdentifier(columnName)
            : $"{alias}.{SqlBuilder.QuoteIdentifier(columnName)}";
}
