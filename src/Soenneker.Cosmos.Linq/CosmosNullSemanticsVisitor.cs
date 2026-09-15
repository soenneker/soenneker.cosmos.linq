using System;
using System.Linq.Expressions;
using System.Reflection;
using CosmosFunctions = Microsoft.Azure.Cosmos.Linq.CosmosLinqExtensions;

namespace Soenneker.Cosmos.Linq;

internal sealed class CosmosNullSemanticsVisitor : ExpressionVisitor
{
    // The visitor has no per-traversal state and can be shared by concurrent callers.
    internal static readonly CosmosNullSemanticsVisitor Instance = new();

    private static readonly MethodInfo _isDefined = ((Func<object, bool>)CosmosFunctions.IsDefined).Method;
    private static readonly MethodInfo _isNull = ((Func<object, bool>)CosmosFunctions.IsNull).Method;

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return base.VisitBinary(node);

        Expression? property = IsLiteralNull(node.Right) ? node.Left : IsLiteralNull(node.Left) ? node.Right : null;
        if (property is null || !IsDocumentProperty(property) || !CanRewriteEquality(node, property.Type))
            return base.VisitBinary(node);

        return CreateNullCheck(property, node.NodeType == ExpressionType.Equal);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Member.Name == nameof(Nullable<int>.HasValue) && node.Expression is { } property &&
            IsNullable(property.Type) && node.Member.DeclaringType == property.Type && IsDocumentProperty(property))
            return CreateNullCheck(property, isNull: false);

        return base.VisitMember(node);
    }

    private static bool CanRewriteEquality(BinaryExpression node, Type propertyType)
    {
        if (node.Method is null || node.Method.DeclaringType == typeof(string))
            return true;

        // For lifted equality returning bool, comparing to null does not invoke the underlying value operator.
        if (node.IsLifted && !node.IsLiftedToNull && IsNullable(propertyType))
            return true;

        return !propertyType.IsValueType && node.Method.DeclaringType == propertyType &&
               node.Left.Type == propertyType && node.Right.Type == propertyType && CosmosNullSemantics.IsRegistered(propertyType);
    }

    private static Expression CreateNullCheck(Expression property, bool isNull)
    {
        // Reference types already satisfy the object parameter; only value types need boxing nodes.
        Expression boxed = property.Type.IsValueType ? Expression.Convert(property, typeof(object)) : property;
        MethodCallExpression defined = Expression.Call(_isDefined, boxed);
        MethodCallExpression nullTest = Expression.Call(_isNull, boxed);

        // Type predicates produce booleans even for undefined values, including under negation.
        // Using IsNull also makes repeated rewriting idempotent.
        return isNull
            ? Expression.OrElse(Expression.Not(defined), nullTest)
            : Expression.AndAlso(defined, Expression.Not(nullTest));
    }

    private static bool IsLiteralNull(Expression expression) => expression is ConstantExpression { Value: null } ||
                                                                expression is DefaultExpression &&
                                                                (!expression.Type.IsValueType ||
                                                                 IsNullable(expression.Type));

    // Unlike GetUnderlyingType, this does not allocate an array of generic type arguments.
    private static bool IsNullable(Type type) => type.IsValueType && type.IsGenericType &&
                                                 type.GetGenericTypeDefinition() == typeof(Nullable<>);

    private static bool IsDocumentProperty(Expression expression)
    {
        // Only paths rooted in a lambda parameter; never execute closure fields or property getters.
        if (expression is not MemberExpression)
            return false;

        if (expression.Type.IsValueType && !IsNullable(expression.Type))
            return false;

        while (expression is MemberExpression member)
        {
            // Nullable.Value/HasValue are CLR helpers, not persisted JSON properties.
            if (member.Expression is null || IsNullable(member.Expression.Type))
                return false;
            expression = member.Expression;
        }

        return expression is ParameterExpression;
    }
}
