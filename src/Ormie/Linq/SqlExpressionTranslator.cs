using System.Linq.Expressions;
using System.Reflection;
using Ormie.Mapping;

namespace Ormie.Linq;

internal sealed class SqlExpressionTranslator
{
    private readonly IReadOnlyList<EntityAlias> _entities;
    private readonly SqlBuilder _builder = new();

    public SqlExpressionTranslator(EntityMap map, ParameterExpression parameter, string alias = "")
        : this([new EntityAlias(map, alias, parameter)])
    {
    }

    public SqlExpressionTranslator(IReadOnlyList<EntityAlias> entities)
    {
        _entities = entities;
    }

    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) Translate(
        Expression expression,
        ParameterExpression entityParameter)
    {
        var entity = _entities.First(e => e.Parameter == entityParameter);
        _builder.Sql.Clear();
        _builder.Parameters.Clear();
        Visit(expression, entityParameter);
        return (_builder.Sql.ToString(), _builder.Parameters);
    }

    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) Translate(
        Expression expression,
        ParameterExpression leftParameter,
        ParameterExpression rightParameter)
    {
        _builder.Sql.Clear();
        _builder.Parameters.Clear();
        Visit(expression, leftParameter, rightParameter);
        return (_builder.Sql.ToString(), _builder.Parameters);
    }

    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) TranslateColumn(
        ParameterExpression entityParameter,
        string propertyName)
    {
        _builder.Sql.Clear();
        _builder.Parameters.Clear();
        var entity = GetEntity(entityParameter);
        _builder.Sql.Append(SqlFormatting.QualifyColumn(entity.Alias, GetColumnName(entity.Map, propertyName)));
        return (_builder.Sql.ToString(), _builder.Parameters);
    }

    public string TranslateMemberSelector(Expression expression, ParameterExpression entityParameter)
    {
        _builder.Sql.Clear();
        _builder.Parameters.Clear();
        Visit(expression, entityParameter);
        return _builder.Sql.ToString();
    }

    private void Visit(Expression expression, params ParameterExpression[] parameters)
    {
        switch (expression)
        {
            case BinaryExpression binary:
                VisitBinary(binary, parameters);
                break;
            case MemberExpression member:
                VisitMember(member, parameters);
                break;
            case ConstantExpression constant:
                VisitConstant(constant);
                break;
            case UnaryExpression unary:
                VisitUnary(unary, parameters);
                break;
            case MethodCallExpression methodCall:
                VisitMethodCall(methodCall, parameters);
                break;
            default:
                throw new LinqTranslationException($"Unsupported expression node: {expression.NodeType}.");
        }
    }

    private void VisitBinary(BinaryExpression node, ParameterExpression[] parameters)
    {
        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            _builder.Sql.Append('(');
            Visit(node.Left, parameters);
            _builder.Sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right, parameters);
            _builder.Sql.Append(')');
            return;
        }

        _builder.Sql.Append('(');
        Visit(node.Left, parameters);
        _builder.Sql.Append(' ');
        _builder.Sql.Append(node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.Add => "+",
            ExpressionType.Subtract => "-",
            ExpressionType.Multiply => "*",
            ExpressionType.Divide => "/",
            _ => throw new LinqTranslationException($"Unsupported binary operator: {node.NodeType}.")
        });
        _builder.Sql.Append(' ');
        Visit(node.Right, parameters);
        _builder.Sql.Append(')');
    }

    private void VisitUnary(UnaryExpression node, ParameterExpression[] parameters)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _builder.Sql.Append("(NOT ");
            Visit(node.Operand, parameters);
            _builder.Sql.Append(')');
            return;
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand, parameters);
            return;
        }

        throw new LinqTranslationException($"Unsupported unary operator: {node.NodeType}.");
    }

    private void VisitMember(MemberExpression node, ParameterExpression[] parameters)
    {
        if (TryAppendEntityColumn(node, parameters))
        {
            return;
        }

        _builder.Sql.Append(_builder.AddParameter(EvaluateExpression(node)));
    }

    private void VisitConstant(ConstantExpression node)
    {
        _builder.Sql.Append(_builder.AddParameter(node.Value));
    }

    private void VisitMethodCall(MethodCallExpression node, ParameterExpression[] parameters)
    {
        if (node.Method.Name == nameof(string.Contains) && node.Object is not null)
        {
            Visit(node.Object, parameters);
            _builder.Sql.Append(" LIKE ");
            _builder.Sql.Append(_builder.AddParameter($"%{GetStringArgument(node)}%"));
            return;
        }

        if (node.Method.Name == nameof(string.StartsWith) && node.Object is not null)
        {
            Visit(node.Object, parameters);
            _builder.Sql.Append(" LIKE ");
            _builder.Sql.Append(_builder.AddParameter($"{GetStringArgument(node)}%"));
            return;
        }

        if (node.Method.Name == nameof(string.EndsWith) && node.Object is not null)
        {
            Visit(node.Object, parameters);
            _builder.Sql.Append(" LIKE ");
            _builder.Sql.Append(_builder.AddParameter($"%{GetStringArgument(node)}"));
            return;
        }

        if (node.Method.Name == nameof(string.ToLower) && node.Object is not null && node.Arguments.Count == 0)
        {
            _builder.Sql.Append("LOWER(");
            Visit(node.Object, parameters);
            _builder.Sql.Append(')');
            return;
        }

        if (node.Method.Name == nameof(string.ToUpper) && node.Object is not null && node.Arguments.Count == 0)
        {
            _builder.Sql.Append("UPPER(");
            Visit(node.Object, parameters);
            _builder.Sql.Append(')');
            return;
        }

        if (node.Method.DeclaringType == typeof(string)
            && node.Method.Name == nameof(string.IsNullOrEmpty)
            && node.Object is null
            && node.Arguments.Count == 1)
        {
            Visit(node.Arguments[0], parameters);
            _builder.Sql.Append(" IS NULL OR ");
            Visit(node.Arguments[0], parameters);
            _builder.Sql.Append(" = ");
            _builder.Sql.Append(_builder.AddParameter(string.Empty));
            return;
        }

        throw new LinqTranslationException($"Unsupported method call: {node.Method.Name}.");
    }

    private bool TryAppendEntityColumn(MemberExpression node, ParameterExpression[] parameters)
    {
        foreach (var parameter in parameters)
        {
            if (!IsMemberOnParameter(node, parameter))
            {
                continue;
            }

            var entity = GetEntity(parameter);
            _builder.Sql.Append(SqlFormatting.QualifyColumn(entity.Alias, GetColumnName(entity.Map, node.Member.Name)));
            return true;
        }

        return false;
    }

    private static bool IsMemberOnParameter(MemberExpression node, ParameterExpression parameter) =>
        node.Expression == parameter
        || (node.Expression is MemberExpression parent && IsMemberOnParameter(parent, parameter));

    private EntityAlias GetEntity(ParameterExpression parameter) =>
        _entities.First(e => e.Parameter == parameter);

    private static string GetColumnName(EntityMap map, string propertyName)
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
}
