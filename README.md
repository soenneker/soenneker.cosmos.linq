[![](https://img.shields.io/nuget/v/soenneker.cosmos.linq.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.cosmos.linq/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.linq/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.linq/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.cosmos.linq.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.cosmos.linq/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Cosmos.Linq
### Cosmos DB LINQ extensions, expression rewriting, and tools.

## Installation

```
dotnet add package Soenneker.Cosmos.Linq
```

## Null and undefined semantics

Cosmos distinguishes explicit JSON `null` from an omitted (undefined) property. Use
`WithNullSemantics()` to treat both as null in property comparisons:

```csharp
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Cosmos.Linq;

var query = container.GetItemLinqQueryable<MyDocument>()
    .Where(d => d.Name == null)
    .WithNullSemantics();

using var iterator = query.ToFeedIterator();
```

The rewrite uses SDK type predicates:

| Original | Rewritten |
| --- | --- |
| `d.Name == null` | `!d.Name.IsDefined() || d.Name.IsNull()` |
| `d.Name != null` | `d.Name.IsDefined() && !d.Name.IsNull()` |
| `d.ReleasedAt.HasValue` | `d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull()` |

This also handles reversed comparisons, nullable value properties, nested member
paths, compound predicates, negation, and predicates inside collection queries.
Explicit `IsNull()` and `IsDefined()` calls are preserved. Reapplying the extension
does not add duplicate checks. Nullable `DateTimeOffset`, `DateTime`, `Guid`, and
`decimal` comparisons are supported, including their lifted equality operators.
Negated `HasValue` matches missing properties and explicit JSON null alike.

### Reusable predicates

```csharp
Expression<Func<MyDocument, bool>> predicate = d => d.Name == null;
var rewritten = predicate.WithNullSemantics();
var query = container.GetItemLinqQueryable<MyDocument>().Where(rewritten);
```

### Generated enum-value classes

Register reference types with null-safe equality operators once during application startup:

```csharp
CosmosNullSemantics.RegisterNullComparableType<OutboundDeliverySource>();
```

After registration, `d.Source == null` and `d.Source != null` are normalized too.
Registration is process-wide, thread-safe, and idempotent. It applies to that exact
type, not derived types, and only to same-type null comparisons using operators
declared by that type. Comparisons to actual enum values retain their meaning.
Only register types where equality with null means reference null; arbitrary custom
operators remain excluded. Register before rewriting; existing expressions are not
updated retroactively. The library does not take a runtime dependency on the enum generator.

### Scope

- Apply the query extension **after composing filters** and before `ToFeedIterator()`,
  `ToQueryDefinition()`, or an SDK aggregate such as `CountAsync()`. It rewrites the
  current expression; it does not intercept filters added later.
- The original query provider creates the rewritten query, preserving Cosmos SDK
  execution and translation support. No data is loaded or queries executed by the extension.
- The query overload rejects other providers and wrappers with `ArgumentException`,
  even when no rewrite is needed. It validates the SDK's native query type using
  cached metadata once per result type. This check depends on the SDK's internal type
  identity; SDK upgrades should run the translation tests. The predicate overload
  does not require a provider.
- Rewriting targets literal null (including nullable/reference `default` expressions)
  comparisons on member paths rooted in lambda parameters. Captured variables,
  method results, indexers, converted member paths, and unregistered custom
  reference-type equality operators are not normalized. Nullable property `HasValue`
  checks are supported; captured-variable `HasValue` checks are left alone.
- Captured values and property getters are never evaluated by the rewriter.
- Expressions containing the Cosmos type predicates are for Cosmos LINQ translation;
  do not compile them for in-memory execution.
- This package does not automatically enable rewriting in `Soenneker.Cosmos.Repository`.

### Validation

Regression tests compare generated SQL with explicit Cosmos predicates, exercise
actual generated enum-value types, dynamic typed-null filters, nested collections,
projections, existing undefined guards, concurrent use, and provider rejection.
Local truth-table tests model missing, null, and populated fields. These tests do
not execute queries against a live Cosmos database.
