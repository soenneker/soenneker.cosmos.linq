using System.Linq.Expressions;
using CosmosFunctions = Microsoft.Azure.Cosmos.Linq.CosmosLinqExtensions;

namespace Soenneker.Cosmos.Linq.Tests;

internal sealed class CosmosTypePredicateValues(bool defined, bool isNull) : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(CosmosFunctions))
        {
            if (node.Method.Name == nameof(CosmosFunctions.IsDefined))
                return Expression.Constant(defined);
            if (node.Method.Name == nameof(CosmosFunctions.IsNull))
                return Expression.Constant(isNull);
        }

        return base.VisitMethodCall(node);
    }
}
