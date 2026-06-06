using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Ormie.Mapping;

namespace Ormie.Linq;

internal sealed class SqlExpressionTranslator(EntityMap map)
{
    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.Ordinal);
    private ParameterExpression? _entityParameter;
    private int _parameterIndex;

    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) Translate(
        Expression expression,
        ParameterExpression entityParameter)
    {
        _entityParameter = entityParameter;
        Visit(expression);
        return (_sql.ToString(), _parameters);
    }

    public string TranslateMemberSelector(Expression expression, ParameterExpression entityParameter)
    {
        _entityParameter = entityParameter;
        _sql.Clear();

        if (expression is LambdaExpression lambda)
        {
            Visit(lambda.Body);
        }
        else
        {
            Visit(expression);
        }

        return _sql.ToString();
    }

    private void Visit(Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                VisitBinary(binary);
                break;
            case MemberExpression member:
                VisitMember(member);
                break;
            case ConstantExpression constant:
                VisitConstant(constant);
                break;
            case UnaryExpression unary:
                VisitUnary(unary);
                break;
            case MethodCallExpression methodCall:
                VisitMethodCall(methodCall);
                break;
            default:
                throw new LinqTranslationException($"Unsupported expression node: {expression.NodeType}.");
        }
    }

    private void VisitBinary(BinaryExpression node)
    {
        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            _sql.Append('(');
            Visit(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right);
            _sql.Append(')');
            return;
        }

        _sql.Append('(');
        Visit(node.Left);
        _sql.Append(' ');
        _sql.Append(node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new LinqTranslationException($"Unsupported binary operator: {node.NodeType}.")
        });
        _sql.Append(' ');
        Visit(node.Right);
        _sql.Append(')');
    }

    private void VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("(NOT ");
            Visit(node.Operand);
            _sql.Append(')');
            return;
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand);
            return;
        }

        throw new LinqTranslationException($"Unsupported unary operator: {node.NodeType}.");
    }

    private void VisitMember(MemberExpression node)
    {
        if (IsEntityMember(node))
        {
            _sql.Append(QuoteIdentifier(GetColumnName(node.Member.Name)));
            return;
        }

        AddParameter(EvaluateExpression(node));
    }

    private void VisitConstant(ConstantExpression node)
    {
        AddParameter(node.Value);
    }

    private void VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(string.Contains) && node.Object is not null)
        {
            Visit(node.Object);
            _sql.Append(" LIKE ");
            var pattern = $"%{GetStringArgument(node)}%";
            AddParameter(pattern);
            return;
        }

        if (node.Method.Name == nameof(string.StartsWith) && node.Object is not null)
        {
            Visit(node.Object);
            _sql.Append(" LIKE ");
            AddParameter($"{GetStringArgument(node)}%");
            return;
        }

        if (node.Method.Name == nameof(string.EndsWith) && node.Object is not null)
        {
            Visit(node.Object);
            _sql.Append(" LIKE ");
            AddParameter($"%{GetStringArgument(node)}");
            return;
        }

        throw new LinqTranslationException($"Unsupported method call: {node.Method.Name}.");
    }

    private bool IsEntityMember(MemberExpression node) =>
        node.Expression == _entityParameter
        || (node.Expression is MemberExpression parent && IsEntityMember(parent));

    private string GetColumnName(string propertyName)
    {
        var property = map.Properties.FirstOrDefault(p => p.Property.Name == propertyName)
            ?? throw new LinqTranslationException($"Property '{propertyName}' is not mapped.");

        return property.ColumnName;
    }

    private static string GetStringArgument(MethodCallExpression node)
    {
        if (node.Arguments.Count != 1)
        {
            throw new LinqTranslationException($"Unsupported argument count for method '{node.Method.Name}'.");
        }

        return Convert.ToString(EvaluateExpression(node.Arguments[0]))
            ?? throw new LinqTranslationException($"Method '{node.Method.Name}' requires a non-null string argument.");
    }

    private static object? EvaluateExpression(Expression expression) =>
        expression switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression member => EvaluateMember(member),
            _ => Expression.Lambda(expression).Compile().DynamicInvoke()
        };

    private static object? EvaluateMember(MemberExpression member)
    {
        object? target = member.Expression switch
        {
            null => null,
            ConstantExpression constant => constant.Value,
            MemberExpression parent => EvaluateMember(parent),
            _ => throw new LinqTranslationException("Unsupported closure member access.")
        };

        return member.Member switch
        {
            FieldInfo field => field.GetValue(target),
            PropertyInfo property => property.GetValue(target),
            _ => throw new LinqTranslationException("Unsupported member type.")
        };
    }

    private void AddParameter(object? value)
    {
        var name = $"p{_parameterIndex++}";
        _parameters[name] = value;
        _sql.Append('@').Append(name);
    }

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
