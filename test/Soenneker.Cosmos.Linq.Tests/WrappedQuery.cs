using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Soenneker.Cosmos.Linq.Tests;

internal sealed class WrappedQuery<T>(IQueryable<T> inner) : IQueryable<T>
{
    public Type ElementType => inner.ElementType;
    public Expression Expression => inner.Expression;
    public IQueryProvider Provider => inner.Provider;
    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
