using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Soenneker.Cosmos.Linq;

/// <summary>Configures trusted null comparisons for Cosmos expression rewriting.</summary>
public static class CosmosNullSemantics
{
    private static readonly ConcurrentDictionary<Type, byte> _nullComparableTypes = new();
    private static readonly ConcurrentDictionary<Type, bool> _enumValueTypes = new();

    /// <summary>
    /// Allows null comparisons using equality operators declared by <typeparamref name="T"/> to be rewritten.
    /// </summary>
    /// <remarks>
    /// Register during application startup, before rewriting queries. Registration is process-wide, thread-safe,
    /// and idempotent. Only register reference types whose equality with null means reference null, such as
    /// generated enum-value classes. This does not change comparisons between non-null values or register derived types.
    /// Registration asserts operator semantics; the library does not execute or inspect the operator implementation.
    /// Previously rewritten expressions are not updated by registration.
    /// Types marked with Soenneker.Gen.EnumValues.EnumValueAttribute or EnumValueAttribute&lt;TValue&gt;
    /// are recognized automatically and do not require registration.
    /// </remarks>
    public static void RegisterNullComparableType<T>() where T : class
    {
        _nullComparableTypes.TryAdd(typeof(T), 0);
    }

    internal static bool IsNullComparable(Type type) => _nullComparableTypes.ContainsKey(type) ||
                                                       _enumValueTypes.GetOrAdd(type, static candidate => IsEnumValue(candidate));

    private static bool IsEnumValue(Type type)
    {
        // Read only directly declared metadata, without constructing attributes or depending on the generator.
        foreach (CustomAttributeData attribute in type.GetCustomAttributesData())
        {
            Type attributeType = attribute.AttributeType;
            if (attributeType.IsGenericType)
                attributeType = attributeType.GetGenericTypeDefinition();

            if (attributeType.FullName is "Soenneker.Gen.EnumValues.EnumValueAttribute" or
                "Soenneker.Gen.EnumValues.EnumValueAttribute`1")
                return true;
        }

        return false;
    }
}
