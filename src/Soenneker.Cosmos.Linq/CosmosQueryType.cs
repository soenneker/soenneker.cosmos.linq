using System;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Linq;

internal static class CosmosQueryType<T>
{
    // The SDK's iterator/SQL extensions require this internal concrete type too. Resolve once per T,
    // without creating a client, translating a query, or allocating an iterator on each validation.
    internal static readonly Type? Value = typeof(CosmosClient).Assembly
        .GetType("Microsoft.Azure.Cosmos.Linq.CosmosLinqQuery`1")?.MakeGenericType(typeof(T));
}
