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

### What gets rewritten

Cosmos treats a missing property differently from an explicit JSON `null`.
`WithNullSemantics()` rewrites your filters so both count as null:

| Your filter | Rewritten filter |
| --- | --- |
| `d.Name == null` | `!d.Name.IsDefined() \|\| d.Name.IsNull()` |
| `d.Name != null` | `d.Name.IsDefined() && !d.Name.IsNull()` |
| `d.ReleasedAt.HasValue` | `d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull()` |
| `d.Source == null` | `!d.Source.IsDefined() \|\| d.Source.IsNull()` |

`IsDefined()` checks whether the JSON property exists; `IsNull()` checks whether
its value is JSON null. The Cosmos SDK translates these into `IS_DEFINED(...)`
and `IS_NULL(...)` in SQL. Equality matches missing or null properties;
inequality and `HasValue` require a present, non-null value.

Works with nullable value types, nested properties, and collection predicates.
Types generated with `[EnumValue]` or `[EnumValue<T>]` work automatically—no registration needed.

### When to call it

The extension rewrites the expression that exists **when you call it**. It returns
a query with those changes; filters added afterward are not automatically rewritten.

```csharp
var query = container.GetItemLinqQueryable<MyDocument>();
query = query.Where(d => d.Name == null);
query = query.Where(d => d.Source != null);

// Rewrite after composing filters, immediately before execution.
query = query.WithNullSemantics();
using var iterator = query.ToFeedIterator();
```

If you add more filters later, call it again before execution. Repeated calls
do not duplicate the checks. In a repository, apply it inside the methods that
execute queries, before `ToFeedIterator()`, `ToQueryDefinition()`, or `CountAsync()`.
Calling it only when creating the initial query will miss filters added by callers.

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
- Checks against literal `null` and nullable `.HasValue` are supported. Captured variables and method results are left alone.
- Explicit `IsNull()` and `IsDefined()` checks keep their original meaning.
