using System;
using System.Linq;
using System.Linq.Expressions;

namespace Soenneker.Cosmos.Linq;

/// <summary>
/// Cosmos DB LINQ extensions, expression rewriting, and tools.
/// </summary>
public static class CosmosLinqExtensions
{
    /// <summary>
    /// Rewrites property comparisons with literal null so missing Cosmos properties count as null.
    /// </summary>
    /// <remarks>
    /// Apply after composing the query and before executing it. Later comparisons are not rewritten automatically.
    /// The original provider is preserved. The resulting expression is intended for Cosmos LINQ, not in-memory execution.
    /// Supports nullable value-type equality, nullable HasValue, and registered reference-type null equality.
    /// Captured values, method results, unregistered custom equality, and explicit Cosmos type checks are left unchanged.
    /// </remarks>
    /// <exception cref="ArgumentException">The query is not a native Cosmos SDK LINQ query, including provider wrappers.</exception>
    public static IQueryable<T> WithNullSemantics<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.GetType() != CosmosQueryType<T>.Value)
            throw new ArgumentException("WithNullSemantics requires a native Microsoft.Azure.Cosmos LINQ query. " +
                                        "Other providers and query wrappers are not supported.", nameof(query));

        Expression original = query.Expression;
        Expression expression = CosmosNullSemanticsVisitor.Instance.Visit(original);
        return ReferenceEquals(expression, original) ? query : query.Provider.CreateQuery<T>(expression);
    }

    /// <summary>
    /// Rewrites literal null comparisons on document properties in a predicate for Cosmos LINQ.
    /// </summary>
    /// <remarks>
    /// Missing properties count as null. This does not evaluate captured values or support in-memory execution.
    /// Explicit IsNull and IsDefined calls retain their original meaning.
    /// </remarks>
    public static Expression<Func<T, bool>> WithNullSemantics<T>(this Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return (Expression<Func<T, bool>>)CosmosNullSemanticsVisitor.Instance.Visit(predicate);
    }
}
