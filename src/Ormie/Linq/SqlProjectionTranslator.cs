using System.Linq.Expressions;
using Ormie.Mapping;

namespace Ormie.Linq;

internal sealed class SqlProjectionTranslator(IReadOnlyList<EntityAlias> entities)
{
    public (string SelectClause, IReadOnlyList<string> ResultProperties) Translate<T>(
        Expression<Func<T, object>> selector)
    {
        var lambda = StripConvert(selector);
        return TranslateExpression(lambda.Body);
    }

    public (string SelectClause, IReadOnlyList<string> ResultProperties) Translate<TLeft, TRight>(
        Expression<Func<TLeft, TRight, object>> selector)
    {
        var lambda = StripConvert(selector);
        return TranslateExpression(lambda.Body);
    }

    private static LambdaExpression StripConvert(LambdaExpression selector)
    {
        if (selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            return Expression.Lambda(unary.Operand, selector.Parameters);
        }

        return selector;
    }

    private (string SelectClause, IReadOnlyList<string> ResultProperties) TranslateExpression(Expression expression)
    {
        return expression switch
        {
            NewExpression newExpression => TranslateNew(newExpression),
            MemberInitExpression memberInit => TranslateMemberInit(memberInit),
            MemberExpression member => TranslateSingleMember(member),
            _ => throw new LinqTranslationException($"Unsupported projection expression: {expression.NodeType}.")
        };
    }

    private (string SelectClause, IReadOnlyList<string> ResultProperties) TranslateNew(NewExpression expression)
    {
        var columns = new List<string>();
        var properties = new List<string>();

        for (var i = 0; i < expression.Arguments.Count; i++)
        {
            var argument = expression.Arguments[i];
            var propertyName = expression.Members?[i].Name ?? $"Item{i}";
            columns.Add(BuildColumnSql(argument, propertyName));
            properties.Add(propertyName);
        }

        return (string.Join(", ", columns), properties);
    }

    private (string SelectClause, IReadOnlyList<string> ResultProperties) TranslateMemberInit(MemberInitExpression expression)
    {
        var columns = new List<string>();
        var properties = new List<string>();

        foreach (var binding in expression.Bindings.OfType<MemberAssignment>())
        {
            var propertyName = binding.Member.Name;
            columns.Add(BuildColumnSql(binding.Expression, propertyName));
            properties.Add(propertyName);
        }

        return (string.Join(", ", columns), properties);
    }

    private (string SelectClause, IReadOnlyList<string> ResultProperties) TranslateSingleMember(MemberExpression expression)
    {
        var propertyName = expression.Member.Name;
        return (BuildColumnSql(expression, propertyName), [propertyName]);
    }

    private string BuildColumnSql(Expression expression, string resultProperty)
    {
        if (expression is MemberExpression member && member.Expression is ParameterExpression parameter)
        {
            var translator = new SqlExpressionTranslator(entities);
            var (sql, _) = translator.TranslateColumn(parameter, member.Member.Name);
            return $"{sql} AS {SqlBuilder.QuoteIdentifier(resultProperty)}";
        }

        var valueTranslator = new SqlExpressionTranslator(entities);
        var (valueSql, _) = valueTranslator.Translate(expression, entities[0].Parameter);
        return $"{valueSql} AS {SqlBuilder.QuoteIdentifier(resultProperty)}";
    }
}
