using System.Linq.Expressions;
using System.Text;
using Ormie.Mapping;

namespace Ormie.Linq;

public sealed class OrmQuery<T>
{
    private readonly Ormie _orm;
    private readonly EntityMap _map;
    private readonly ParameterExpression _parameter = Expression.Parameter(typeof(T), "x");
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
        var normalized = Expression.Lambda<Func<T, bool>>(
            new ParameterReplacer(predicate.Parameters[0], _parameter).Visit(predicate.Body),
            _parameter);
        _where = _where is null ? normalized : CombineAnd(_where, normalized);
        return this;
    }

    public OrmQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var normalized = Expression.Lambda<Func<T, TKey>>(
            new ParameterReplacer(keySelector.Parameters[0], _parameter).Visit(keySelector.Body),
            _parameter);
        _orderings.Add((normalized, false));
        return this;
    }

    public OrmQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var normalized = Expression.Lambda<Func<T, TKey>>(
            new ParameterReplacer(keySelector.Parameters[0], _parameter).Visit(keySelector.Body),
            _parameter);
        _orderings.Add((normalized, true));
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

    public OrmProjectedQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new OrmProjectedQuery<TResult>(_orm, () =>
        {
            var entities = CreateAliases();
            var normalizedBody = new ParameterReplacer(selector.Parameters[0], _parameter).Visit(selector.Body);
            var projection = new SqlProjectionTranslator(entities).Translate<T>(
                Expression.Lambda<Func<T, object>>(Expression.Convert(normalizedBody, typeof(object)), _parameter));
            var sql = new StringBuilder();
            sql.Append("SELECT ").Append(projection.SelectClause);
            sql.Append(" FROM ").Append(SqlBuilder.QuoteIdentifier(_map.TableName));
            AppendWhereClause(sql, entities, out var parameters);
            AppendOrderByClause(sql, entities);
            AppendLimitClause(sql, null);
            return (sql.ToString(), parameters);
        });
    }

    public OrmJoinQuery<T, TRight> Join<TRight>(Expression<Func<T, TRight, bool>> joinOn)
    {
        ArgumentNullException.ThrowIfNull(joinOn);
        var rightMap = _orm.GetEntityMap(typeof(TRight));
        var joinQuery = new OrmJoinQuery<T, TRight>(_orm, _map, rightMap, joinOn);

        if (_where is not null)
        {
            joinQuery = joinQuery.Where(ReplaceJoinWhere<TRight>(_where));
        }

        return joinQuery;
    }

    public OrmGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return new OrmGroupedQuery<T, TKey>(_orm, _map, keySelector, _where);
    }

    public Task<IReadOnlyList<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var entities = CreateAliases();
        var (sql, parameters) = BuildSelectSql(entities, null);
        return _orm.QueryWithParametersAsync<T>(sql, parameters, cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var entities = CreateAliases();
        var (sql, parameters) = BuildSelectSql(entities, 1);
        var results = await _orm.QueryWithParametersAsync<T>(sql, parameters, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<T> SingleAsync(CancellationToken cancellationToken = default)
    {
        var entities = CreateAliases();
        var (sql, parameters) = BuildSelectSql(entities, null);
        var results = await _orm.QueryWithParametersAsync<T>(sql, parameters, cancellationToken).ConfigureAwait(false);
        return results.Count switch
        {
            0 => throw new InvalidOperationException("Sequence contains no elements."),
            1 => results[0],
            _ => throw new InvalidOperationException("Sequence contains more than one element.")
        };
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var entities = CreateAliases();
        var (sql, parameters) = BuildCountSql(entities);
        return _orm.ExecuteScalarAsync<int>(sql, parameters, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await CountAsync(cancellationToken).ConfigureAwait(false) > 0;

    public Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var normalized = Expression.Lambda<Func<T, TResult>>(
            new ParameterReplacer(selector.Parameters[0], _parameter).Visit(selector.Body),
            _parameter);
        var entities = CreateAliases();
        var translator = new SqlExpressionTranslator(entities);
        var columnSql = translator.TranslateMemberSelector(normalized.Body, _parameter);
        var (sql, parameters) = BuildAggregateSql(entities, $"MAX({columnSql})");
        return _orm.ExecuteScalarAsync<TResult>(sql, parameters, cancellationToken);
    }

    public Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var normalized = Expression.Lambda<Func<T, TResult>>(
            new ParameterReplacer(selector.Parameters[0], _parameter).Visit(selector.Body),
            _parameter);
        var entities = CreateAliases();
        var translator = new SqlExpressionTranslator(entities);
        var columnSql = translator.TranslateMemberSelector(normalized.Body, _parameter);
        var (sql, parameters) = BuildAggregateSql(entities, $"MIN({columnSql})");
        return _orm.ExecuteScalarAsync<TResult>(sql, parameters, cancellationToken);
    }

    private IReadOnlyList<EntityAlias> CreateAliases() => [new EntityAlias(_map, string.Empty, _parameter)];

    private (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildSelectSql(
        IReadOnlyList<EntityAlias> entities,
        int? limit)
    {
        var sql = new StringBuilder();
        var columns = string.Join(", ", _map.Properties.Select(p => SqlBuilder.QuoteIdentifier(p.ColumnName)));
        sql.Append("SELECT ").Append(columns);
        sql.Append(" FROM ").Append(SqlBuilder.QuoteIdentifier(_map.TableName));
        AppendWhereClause(sql, entities, out var parameters);
        AppendOrderByClause(sql, entities);
        AppendLimitClause(sql, limit);
        return (sql.ToString(), parameters);
    }

    private (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildCountSql(IReadOnlyList<EntityAlias> entities)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT COUNT(*) FROM ").Append(SqlBuilder.QuoteIdentifier(_map.TableName));
        AppendWhereClause(sql, entities, out var parameters);
        return (sql.ToString(), parameters);
    }

    private (string Sql, IReadOnlyDictionary<string, object?> Parameters) BuildAggregateSql(
        IReadOnlyList<EntityAlias> entities,
        string aggregateExpression)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(aggregateExpression);
        sql.Append(" FROM ").Append(SqlBuilder.QuoteIdentifier(_map.TableName));
        AppendWhereClause(sql, entities, out var parameters);
        return (sql.ToString(), parameters);
    }

    private void AppendWhereClause(StringBuilder sql, IReadOnlyList<EntityAlias> entities, out IReadOnlyDictionary<string, object?> parameters)
    {
        parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_where is null)
        {
            return;
        }

        var translator = new SqlExpressionTranslator(entities);
        var (whereSql, whereParameters) = translator.Translate(_where.Body, _parameter);
        sql.Append(" WHERE ").Append(whereSql);
        parameters = whereParameters;
    }

    private void AppendOrderByClause(StringBuilder sql, IReadOnlyList<EntityAlias> entities)
    {
        if (_orderings.Count == 0)
        {
            return;
        }

        sql.Append(" ORDER BY ");
        var orderClauses = _orderings.Select(ordering =>
        {
            var translator = new SqlExpressionTranslator(entities);
            var columnSql = translator.TranslateMemberSelector(ordering.KeySelector.Body, _parameter);
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

    private Expression<Func<T, TRight, bool>> ReplaceJoinWhere<TRight>(Expression<Func<T, bool>> where)
    {
        var left = Expression.Parameter(typeof(T), "l");
        var right = Expression.Parameter(typeof(TRight), "r");
        var body = new ParameterReplacer(where.Parameters[0], left).Visit(where.Body);
        return Expression.Lambda<Func<T, TRight, bool>>(body, left, right);
    }

    private Expression<Func<T, bool>> CombineAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var leftBody = new ParameterReplacer(left.Parameters[0], _parameter).Visit(left.Body);
        var rightBody = new ParameterReplacer(right.Parameters[0], _parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), _parameter);
    }
}
