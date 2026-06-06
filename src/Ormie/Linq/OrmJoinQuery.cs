using System.Linq.Expressions;
using System.Text;
using Ormie.Mapping;

namespace Ormie.Linq;

public sealed class OrmProjectedQuery<TResult>
{
    private readonly Ormie _orm;
    private readonly Func<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> _build;

    internal OrmProjectedQuery(Ormie orm, Func<(string Sql, IReadOnlyDictionary<string, object?> Parameters)> build)
    {
        _orm = orm;
        _build = build;
    }

    public Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _build();
        return _orm.QueryProjectedAsync<TResult>(sql, parameters, cancellationToken);
    }

    public async Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var (sql, parameters) = _build();
        var results = await _orm.QueryProjectedAsync<TResult>($"{sql} LIMIT 1", parameters, cancellationToken)
            .ConfigureAwait(false);
        return results.FirstOrDefault();
    }
}

public sealed class OrmJoinQuery<TLeft, TRight>
{
    private readonly Ormie _orm;
    private readonly EntityMap _leftMap;
    private readonly EntityMap _rightMap;
    private readonly ParameterExpression _leftParameter;
    private readonly ParameterExpression _rightParameter;
    private readonly Expression<Func<TLeft, TRight, bool>> _joinOn;
    private Expression<Func<TLeft, TRight, bool>>? _where;

    internal OrmJoinQuery(
        Ormie orm,
        EntityMap leftMap,
        EntityMap rightMap,
        Expression<Func<TLeft, TRight, bool>> joinOn)
    {
        _orm = orm;
        _leftMap = leftMap;
        _rightMap = rightMap;
        _leftParameter = Expression.Parameter(typeof(TLeft), "l");
        _rightParameter = Expression.Parameter(typeof(TRight), "r");
        _joinOn = NormalizeJoinExpression(joinOn);
    }

    public OrmJoinQuery<TLeft, TRight> Where(Expression<Func<TLeft, TRight, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var normalized = NormalizeJoinExpression(predicate);
        _where = _where is null ? normalized : CombineAnd(_where, normalized);
        return this;
    }

    public OrmProjectedQuery<TResult> Select<TResult>(Expression<Func<TLeft, TRight, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new OrmProjectedQuery<TResult>(_orm, () =>
        {
            var entities = CreateAliases();
            var normalizedBody = new JoinParameterReplacer(
                selector.Parameters[0],
                selector.Parameters[1],
                _leftParameter,
                _rightParameter).Visit(selector.Body);
            var projection = new SqlProjectionTranslator(entities).Translate<TLeft, TRight>(
                Expression.Lambda<Func<TLeft, TRight, object>>(
                    Expression.Convert(normalizedBody, typeof(object)),
                    _leftParameter,
                    _rightParameter));
            var sql = new StringBuilder();
            sql.Append("SELECT ").Append(projection.SelectClause);
            AppendFromJoin(sql);
            AppendWhere(sql, entities);
            return (sql.ToString(), BuildParameters(entities));
        });
    }

    public Task<IReadOnlyList<TLeft>> SelectLeftAsync(CancellationToken cancellationToken = default)
    {
        var entities = CreateAliases();
        var columns = string.Join(", ", _leftMap.Properties.Select(p =>
            $"{SqlFormatting.QualifyColumn("l", p.ColumnName)} AS {SqlBuilder.QuoteIdentifier(p.ColumnName)}"));
        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(columns);
        AppendFromJoin(sql);
        AppendWhere(sql, entities);
        return _orm.QueryWithParametersAsync<TLeft>(sql.ToString(), BuildParameters(entities), cancellationToken);
    }

    private IReadOnlyList<EntityAlias> CreateAliases() =>
    [
        new EntityAlias(_leftMap, "l", _leftParameter),
        new EntityAlias(_rightMap, "r", _rightParameter)
    ];

    private void AppendFromJoin(StringBuilder sql)
    {
        sql.Append(" FROM ")
            .Append(SqlBuilder.QuoteIdentifier(_leftMap.TableName))
            .Append(" l INNER JOIN ")
            .Append(SqlBuilder.QuoteIdentifier(_rightMap.TableName))
            .Append(" r ON ");

        var entities = CreateAliases();
        var translator = new SqlExpressionTranslator(entities);
        var (joinSql, _) = translator.Translate(_joinOn.Body, _leftParameter, _rightParameter);
        sql.Append(joinSql);
    }

    private void AppendWhere(StringBuilder sql, IReadOnlyList<EntityAlias> entities)
    {
        if (_where is null)
        {
            return;
        }

        var translator = new SqlExpressionTranslator(entities);
        var (whereSql, _) = translator.Translate(_where.Body, _leftParameter, _rightParameter);
        sql.Append(" WHERE ").Append(whereSql);
    }

    private IReadOnlyDictionary<string, object?> BuildParameters(IReadOnlyList<EntityAlias> entities)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        var joinTranslator = new SqlExpressionTranslator(entities);
        MergeParameters(parameters, joinTranslator.Translate(_joinOn.Body, _leftParameter, _rightParameter).Parameters);

        if (_where is not null)
        {
            var whereTranslator = new SqlExpressionTranslator(entities);
            MergeParameters(parameters, whereTranslator.Translate(_where.Body, _leftParameter, _rightParameter).Parameters);
        }

        return parameters;
    }

    private static void MergeParameters(Dictionary<string, object?> target, IReadOnlyDictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }

    private Expression<Func<TLeft, TRight, bool>> NormalizeJoinExpression(
        Expression<Func<TLeft, TRight, bool>> expression) =>
        Expression.Lambda<Func<TLeft, TRight, bool>>(
            new JoinParameterReplacer(
                expression.Parameters[0],
                expression.Parameters[1],
                _leftParameter,
                _rightParameter).Visit(expression.Body),
            _leftParameter,
            _rightParameter);

    private Expression<Func<TLeft, TRight, bool>> CombineAnd(
        Expression<Func<TLeft, TRight, bool>> left,
        Expression<Func<TLeft, TRight, bool>> right)
    {
        var leftBody = ReplaceParameters(left.Body, left.Parameters[0], left.Parameters[1], _leftParameter, _rightParameter);
        var rightBody = ReplaceParameters(right.Body, right.Parameters[0], right.Parameters[1], _leftParameter, _rightParameter);
        return Expression.Lambda<Func<TLeft, TRight, bool>>(Expression.AndAlso(leftBody, rightBody), _leftParameter, _rightParameter);
    }

    private static Expression ReplaceParameters(
        Expression expression,
        ParameterExpression leftSource,
        ParameterExpression rightSource,
        ParameterExpression leftTarget,
        ParameterExpression rightTarget) =>
        new JoinParameterReplacer(leftSource, rightSource, leftTarget, rightTarget).Visit(expression);
}

public sealed class OrmGroupedQuery<T, TKey>
{
    private readonly Ormie _orm;
    private readonly EntityMap _map;
    private readonly Expression<Func<T, TKey>> _keySelector;
    private Expression<Func<T, bool>>? _where;

    internal OrmGroupedQuery(Ormie orm, EntityMap map, Expression<Func<T, TKey>> keySelector, Expression<Func<T, bool>>? where)
    {
        _orm = orm;
        _map = map;
        _keySelector = keySelector;
        _where = where;
    }

    public OrmGroupedQuery<T, TKey> Where(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _where = _where is null ? predicate : CombineAnd(_where, predicate);
        return this;
    }

    private static Expression<Func<T, bool>> CombineAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var leftBody = new ParameterReplacer(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
    }

    public Task<IReadOnlyList<GroupCount<TKey>>> ToCountListAsync(CancellationToken cancellationToken = default)
    {
        var parameter = _keySelector.Parameters[0];
        var translator = new SqlExpressionTranslator(_map, parameter);
        var keySql = translator.TranslateMemberSelector(_keySelector.Body, parameter);

        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(keySql).Append(" AS ")
            .Append(SqlBuilder.QuoteIdentifier("Key"))
            .Append(", COUNT(*) AS ")
            .Append(SqlBuilder.QuoteIdentifier("Count"))
            .Append(" FROM ")
            .Append(SqlBuilder.QuoteIdentifier(_map.TableName));

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_where is not null)
        {
            var whereTranslator = new SqlExpressionTranslator(_map, parameter);
            var (whereSql, whereParameters) = whereTranslator.Translate(_where.Body, parameter);
            sql.Append(" WHERE ").Append(whereSql);
            foreach (var (key, value) in whereParameters)
            {
                parameters[key] = value;
            }
        }

        sql.Append(" GROUP BY ").Append(keySql);
        return _orm.QueryProjectedAsync<GroupCount<TKey>>(sql.ToString(), parameters, cancellationToken);
    }
}

public sealed class GroupCount<TKey>
{
    public TKey Key { get; set; } = default!;

    public int Count { get; set; }
}

internal sealed class JoinParameterReplacer(
    ParameterExpression leftSource,
    ParameterExpression rightSource,
    ParameterExpression leftTarget,
    ParameterExpression rightTarget) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == leftSource)
        {
            return leftTarget;
        }

        if (node == rightSource)
        {
            return rightTarget;
        }

        return base.VisitParameter(node);
    }
}

internal sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == source ? target : base.VisitParameter(node);
}
