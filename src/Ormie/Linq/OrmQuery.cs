using System.Linq.Expressions;
using System.Text;
using Ormie.Mapping;

namespace Ormie.Linq;

public sealed class OrmQuery<T>
{
    private readonly Ormie _orm;
    private readonly EntityMap _map;
    private Expression<Func<T, bool>>? _where;
    private readonly List<(LambdaExpression KeySelector, bool Descending)> _orderings = [];
    private int? _skip;
    private int? _take;

    internal OrmQuery(Ormie orm, EntityMap map)
    {
        _orm = orm;
        _map = map;
    }

    public OrmQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _where = _where is null ? predicate : CombineAnd(_where, predicate);
        return this;
    }

    public OrmQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderings.Add((keySelector, false));
        return this;
    }

    public OrmQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderings.Add((keySelector, true));
        return this;
    }

    public OrmQuery<T> Skip(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _skip = count;
        return this;
    }

    public OrmQuery<T> Take(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _take = count;
        return this;
    }

    public Task<IReadOnlyList<T>> ToListAsync(CancellationToken cancellationToken = default) =>
        _orm.QueryWithParametersAsync<T>(BuildSelectSql(), BuildParameters(), cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(limit: 1);
        var results = await _orm.QueryWithParametersAsync<T>(sql, BuildParameters(), cancellationToken)
            .ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        return await _orm.ExecuteScalarAsync<int>(sql, BuildParameters(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await CountAsync(cancellationToken).ConfigureAwait(false) > 0;

    private IReadOnlyDictionary<string, object?> BuildParameters()
    {
        if (_where is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var translator = new SqlExpressionTranslator(_map);
        return translator.Translate(_where.Body, _where.Parameters[0]).Parameters;
    }

    private string BuildSelectSql(int? limit = null)
    {
        var sql = new StringBuilder();
        var columns = string.Join(", ", _map.Properties.Select(p => QuoteIdentifier(p.ColumnName)));

        sql.Append("SELECT ").Append(columns);
        sql.Append(" FROM ").Append(QuoteIdentifier(_map.TableName));
        AppendWhereClause(sql);
        AppendOrderByClause(sql);
        AppendLimitClause(sql, limit);

        return sql.ToString();
    }

    private string BuildCountSql()
    {
        var sql = new StringBuilder();
        sql.Append("SELECT COUNT(*) FROM ").Append(QuoteIdentifier(_map.TableName));
        AppendWhereClause(sql);
        return sql.ToString();
    }

    private void AppendWhereClause(StringBuilder sql)
    {
        if (_where is null)
        {
            return;
        }

        var translator = new SqlExpressionTranslator(_map);
        var (whereSql, _) = translator.Translate(_where.Body, _where.Parameters[0]);
        sql.Append(" WHERE ").Append(whereSql);
    }

    private void AppendOrderByClause(StringBuilder sql)
    {
        if (_orderings.Count == 0)
        {
            return;
        }

        sql.Append(" ORDER BY ");
        var orderClauses = _orderings.Select(ordering =>
        {
            var translator = new SqlExpressionTranslator(_map);
            var columnSql = translator.TranslateMemberSelector(
                ordering.KeySelector.Body,
                ordering.KeySelector.Parameters[0]);
            return $"{columnSql} {(ordering.Descending ? "DESC" : "ASC")}";
        });

        sql.Append(string.Join(", ", orderClauses));
    }

    private void AppendLimitClause(StringBuilder sql, int? limit)
    {
        var take = limit ?? _take;
        if (take.HasValue)
        {
            sql.Append(" LIMIT ").Append(take.Value);
        }

        if (_skip.HasValue)
        {
            sql.Append(" OFFSET ").Append(_skip.Value);
        }
    }

    private static Expression<Func<T, bool>> CombineAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, ParameterExpression target) =>
        new ParameterReplacer(source, target).Visit(expression);

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}

internal sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == source ? target : base.VisitParameter(node);
}
