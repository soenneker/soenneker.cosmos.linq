using System;
using System.Linq;
using System.Linq.Expressions;
using AwesomeAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Soenneker.Cosmos.Linq.Tests;

public sealed class ApplicationPatternTests
{
    [Test]
    public void NullableValueOperatorsAndHasValueTranslate()
    {
        AssertTranslation(d => d.ReleasedAt == null, d => !d.ReleasedAt.IsDefined() || d.ReleasedAt.IsNull());
        AssertTranslation(d => null == d.ReleasedAt, d => !d.ReleasedAt.IsDefined() || d.ReleasedAt.IsNull());
        AssertTranslation(d => d.ReleasedAt != null, d => d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull());
        AssertTranslation(d => null != d.ReleasedAt, d => d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull());
        AssertTranslation(d => !(d.ReleasedAt == null), d => !(!d.ReleasedAt.IsDefined() || d.ReleasedAt.IsNull()));
        AssertTranslation(d => d.Date == null, d => !d.Date.IsDefined() || d.Date.IsNull());
        AssertTranslation(d => d.Identifier == null, d => !d.Identifier.IsDefined() || d.Identifier.IsNull());
        AssertTranslation(d => d.Amount == null, d => !d.Amount.IsDefined() || d.Amount.IsNull());
        AssertTranslation(d => d.ReleasedAt == default(DateTimeOffset?), d => !d.ReleasedAt.IsDefined() || d.ReleasedAt.IsNull());
        AssertTranslation(d => d.ReleasedAt.HasValue, d => d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull());
        AssertTranslation(d => !d.ReleasedAt.HasValue, d => !(d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull()));
        AssertTranslation(d => d.ReleasedAt.HasValue == false, d => (d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull()) == false);
        AssertTranslation(d => d.ReleasedAt.HasValue != true, d => (d.ReleasedAt.IsDefined() && !d.ReleasedAt.IsNull()) != true);
        AssertTranslation(d => d.Parent!.ReleasedAt.HasValue, d => d.Parent!.ReleasedAt.IsDefined() && !d.Parent.ReleasedAt.IsNull());
    }

    [Test]
    public void GeneratedEnumNullComparisonsTranslateWithoutRegistration()
    {
        AssertTranslation(d => d.Source == null, d => !d.Source.IsDefined() || d.Source.IsNull());
        AssertTranslation(d => null == d.Source, d => !d.Source.IsDefined() || d.Source.IsNull());
        AssertTranslation(d => d.Source != null, d => d.Source.IsDefined() && !d.Source.IsNull());
        AssertTranslation(d => null != d.Source, d => d.Source.IsDefined() && !d.Source.IsNull());
        AssertTranslation(d => d.Status! == null!, d => !d.Status.IsDefined() || d.Status.IsNull());
        AssertTranslation(d => d.Status! != null!, d => d.Status.IsDefined() && !d.Status.IsNull());
        AssertTranslation(d => !(d.Source == null), d => !(!d.Source.IsDefined() || d.Source.IsNull()));
        Expression<Func<ApplicationDocument, bool>> nonNull = d => d.Source == DeliverySource.Manual;
        nonNull.WithNullSemantics().Should().BeSameAs(nonNull);
    }

    [Test]
    public void DynamicTypedNullFiltersTranslate()
    {
        // Same expression shape as AddRequestDataOptions / WhereDynamicEquals.
        ParameterExpression parameter = Expression.Parameter(typeof(ApplicationDocument), "d");
        foreach (string name in new[] { nameof(ApplicationDocument.Name), nameof(ApplicationDocument.ReleasedAt), nameof(ApplicationDocument.Amount) })
        {
            MemberExpression member = Expression.Property(parameter, name);
            var predicate = Expression.Lambda<Func<ApplicationDocument, bool>>(Expression.Equal(member, Expression.Constant(null, member.Type)), parameter);
            var rewritten = predicate.WithNullSemantics();
            using var client = CreateClient();
            string sql = Query(client).Where(rewritten).ToQueryDefinition().QueryText;
            sql.Should().Contain("IS_DEFINED").And.Contain("IS_NULL");
            rewritten.WithNullSemantics().Should().BeSameAs(rewritten);
        }
    }

    [Test]
    public void CollectionGuardsAndExistingUndefinedGuardsTranslate()
    {
        AssertTranslation(d => d.Children != null && d.Children.Any(c => c.ReleasedAt == null),
            d => (d.Children.IsDefined() && !d.Children.IsNull()) && d.Children!.Any(c => !c.ReleasedAt.IsDefined() || c.ReleasedAt.IsNull()));
        AssertTranslation(d => d.Tags == null || !d.Tags.Any(),
            d => (!d.Tags.IsDefined() || d.Tags.IsNull()) || !d.Tags!.Any());
        AssertTranslation(d => !d.Name.IsDefined() || d.Name == null || d.Name == "user",
            d => !d.Name.IsDefined() || (!d.Name.IsDefined() || d.Name.IsNull()) || d.Name == "user");
    }

    [Test]
    public void ProjectionsAndDateRangesRemainComposable()
    {
        using var client = CreateClient();
        var source = Query(client);
        DateTimeOffset start = DateTimeOffset.UnixEpoch;
        var projection = source.Select(d => new { d.Name, d.ReleasedAt }).Where(d => d.ReleasedAt >= start);
        projection.WithNullSemantics().Should().BeSameAs(projection);
        var actual = projection.Where(d => d.Name == null).WithNullSemantics().ToQueryDefinition().QueryText;
        var expected = projection.Where(d => !d.Name.IsDefined() || d.Name.IsNull()).ToQueryDefinition().QueryText;
        actual.Should().Be(expected);
        var computed = source.Select(d => new { Name = d.Name ?? "fallback" }).Where(d => d.Name == null);
        computed.WithNullSemantics().ToQueryDefinition().QueryText.Should().Be(
            source.Select(d => new { Name = d.Name ?? "fallback" }).Where(d => !d.Name.IsDefined() || d.Name.IsNull()).ToQueryDefinition().QueryText);
    }

    [Test]
    public void RejectsInMemoryProviderEvenWithoutNullComparisons()
    {
        var source = Array.Empty<ApplicationDocument>().AsQueryable();
        Action unchanged = () => source.WithNullSemantics();
        Action filtered = () => source.Where(d => d.Name == null).WithNullSemantics();
        unchanged.Should().Throw<ArgumentException>().WithParameterName("query");
        filtered.Should().Throw<ArgumentException>().WithMessage("*native Microsoft.Azure.Cosmos*");
        Expression<Func<ApplicationDocument, bool>> predicate = d => d.Name == null;
        predicate.WithNullSemantics().Should().NotBeSameAs(predicate);
    }

    [Test]
    public void RejectsWrappersEvenWhenTheyExposeACosmosProvider()
    {
        using var client = CreateClient();
        var wrapper = new WrappedQuery<ApplicationDocument>(Query(client));
        Action rewrite = () => wrapper.WithNullSemantics();
        rewrite.Should().Throw<ArgumentException>().WithParameterName("query");
    }

    [Test]
    public void NullChecksHaveConsistentMissingNullAndValueTruthTables()
    {
        (Expression<Func<ApplicationDocument, bool>> predicate, bool whenNull)[] predicates =
        [
            (d => d.ReleasedAt == null, true),
            (d => d.ReleasedAt != null, false),
            (d => d.ReleasedAt.HasValue, false),
            (d => !d.ReleasedAt.HasValue, true),
            (d => !(d.ReleasedAt == null), false),
            (d => !(d.ReleasedAt != null), true)
        ];
        foreach (var (predicate, whenNull) in predicates)
        {
            var rewritten = predicate.WithNullSemantics();
            // Model the SDK type predicates for missing, explicit null, and a present value.
            foreach (var (defined, isNull, expected) in new[] { (false, false, whenNull), (true, true, whenNull), (true, false, !whenNull) })
            {
                var expression = (Expression<Func<ApplicationDocument, bool>>)new CosmosTypePredicateValues(defined, isNull).Visit(rewritten);
                expression.Compile()(new ApplicationDocument()).Should().Be(expected);
            }
        }
    }

    [Test]
    public void CapturedHasValueAndNonNullValueComparisonsAreUnchanged()
    {
        DateTimeOffset? cutoff = null;
        Expression<Func<ApplicationDocument, bool>> captured = d => !cutoff.HasValue || d.ReleasedAt >= cutoff;
        captured.WithNullSemantics().Should().BeSameAs(captured);
        Expression<Func<ApplicationDocument, bool>> value = d => d.Amount == 10m;
        value.WithNullSemantics().Should().BeSameAs(value);
    }

    private static void AssertTranslation(Expression<Func<ApplicationDocument, bool>> original, Expression<Func<ApplicationDocument, bool>> expected)
    {
        using var client = CreateClient();
        var query = Query(client);
        string sql = query.Where(expected).ToQueryDefinition().QueryText;
        var rewritten = query.Where(original).WithNullSemantics();
        rewritten.ToQueryDefinition().QueryText.Should().Be(sql);
        query.Where(original.WithNullSemantics()).ToQueryDefinition().QueryText.Should().Be(sql);
        rewritten.WithNullSemantics().Should().BeSameAs(rewritten);
    }

    private static CosmosClient CreateClient() => new("https://localhost:8081", Convert.ToBase64String(new byte[64]));
    private static IQueryable<ApplicationDocument> Query(CosmosClient client) => client.GetContainer("test", "test").GetItemLinqQueryable<ApplicationDocument>();
}
