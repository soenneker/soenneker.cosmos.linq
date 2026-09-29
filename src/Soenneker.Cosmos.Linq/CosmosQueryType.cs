using System;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Linq;

internal static class CosmosQueryType<T>
{
    internal static bool IsNativeQuery(Type type) =>
        type.IsGenericType &&
        type.Assembly == typeof(CosmosClient).Assembly &&
        type.GetGenericTypeDefinition().FullName == "Microsoft.Azure.Cosmos.Linq.CosmosLinqQuery`1" &&
        type.GenericTypeArguments[0] == typeof(T);
}
