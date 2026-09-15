using System;
using System.Collections.Concurrent;

namespace Soenneker.Cosmos.Linq;

/// <summary>Configures explicitly trusted null comparisons for Cosmos expression rewriting.</summary>
public static class CosmosNullSemantics
{
    private static readonly ConcurrentDictionary<Type, byte> _nullComparableTypes = new();

    /// <summary>
    /// Allows null comparisons using equality operators declared by <typeparamref name="T"/> to be rewritten.
    /// </summary>
    /// <remarks>
    /// Register during application startup, before rewriting queries. Registration is process-wide, thread-safe,
    /// and idempotent. Only register reference types whose equality with null means reference null, such as
    /// generated enum-value classes. This does not change comparisons between non-null values or register derived types.
    /// Registration asserts operator semantics; the library does not execute or inspect the operator implementation.
    /// Previously rewritten expressions are not updated by registration.
    /// </remarks>
    public static void RegisterNullComparableType<T>() where T : class
    {
        _nullComparableTypes.TryAdd(typeof(T), 0);
    }

    internal static bool IsRegistered(Type type) => _nullComparableTypes.ContainsKey(type);
}
