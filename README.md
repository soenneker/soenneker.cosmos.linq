[![](https://img.shields.io/nuget/v/soenneker.cosmos.linq.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.cosmos.linq/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.linq/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.linq/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.cosmos.linq.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.cosmos.linq/)

# Soenneker.Cosmos.Linq

Treat missing Cosmos DB properties as `null` in LINQ queries.

## Install

```shell
dotnet add package Soenneker.Cosmos.Linq
```

## Usage

Call `WithNullSemantics()` after adding filters and before executing the query:

```csharp
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Cosmos.Linq;

var query = container.GetItemLinqQueryable<MyDocument>()
    .Where(d => d.Name == null)
    .WithNullSemantics();

using var iterator = query.ToFeedIterator();
```

Cosmos treats a missing property differently from an explicit JSON `null`.
This extension makes both count as null:

| Filter | Matches |
| --- | --- |
| `d.Name == null` | Missing or null |
| `d.Name != null` | Present and non-null |
| `d.ReleasedAt.HasValue` | Present and non-null |

Works with nullable value types, nested properties, and collection predicates.
Types generated with `[EnumValue]` or `[EnumValue<T>]` work automatically—no registration needed.

### Reusable predicates

```csharp
Expression<Func<MyDocument, bool>> predicate = d => d.Name == null;
var query = container.GetItemLinqQueryable<MyDocument>()
    .Where(predicate.WithNullSemantics());
```

### Other custom types

For other classes that overload `==` / `!=`, register the type at startup only
if comparison with null means reference null:

```csharp
CosmosNullSemantics.RegisterNullComparableType<MyCustomValue>();
```

## Keep in mind

- Use with native Cosmos SDK queries. Rewritten predicates are for Cosmos, not in-memory execution.
- Only filters already in the expression are rewritten. Call the extension after composing them.
- Checks against literal `null` and nullable `.HasValue` are supported. Captured variables and method results are left alone.
- Explicit `IsNull()` and `IsDefined()` checks keep their original meaning.
