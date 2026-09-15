using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Soenneker.Cosmos.Linq.Tests;

public sealed class CosmosLinqExtensionsTests
{
    [Test]
    public void SharedVisitorSupportsConcurrentRewrites()
    {
        Expression<Func<TestDocument, bool>> predicate = d => d.Name == null && d.Age != null;
        string expected = predicate.WithNullSemantics().ToString();
        Parallel.For(0, 1000, _ =>
        {
            var rewritten = predicate.WithNullSemantics();
            rewritten.ToString().Should().Be(expected);
            rewritten.WithNullSemantics().Should().BeSameAs(rewritten);
        });
    }

    [Test]
    public void RewritesTranslateLikeExplicitCosmosPredicates()
    {
        using var client = new CosmosClient("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        IQueryable<TestDocument> source = client.GetContainer("test", "test").GetItemLinqQueryable<TestDocument>();
        (Expression<Func<TestDocument, bool>> original, Expression<Func<TestDocument, bool>> expected)[] cases =
        [
            (d => d.Name == null, d => !d.Name.IsDefined() || d.Name.IsNull()),
            (d => null == d.Name, d => !d.Name.IsDefined() || d.Name.IsNull()),
            (d => d.Name != null, d => d.Name.IsDefined() && !d.Name.IsNull()),
            (d => null != d.Name, d => d.Name.IsDefined() && !d.Name.IsNull()),
            (d => d.Age == null, d => !d.Age.IsDefined() || d.Age.IsNull()),
            (d => d.Age != null, d => d.Age.IsDefined() && !d.Age.IsNull()),
            (d => d.Parent!.Name == null, d => !d.Parent!.Name.IsDefined() || d.Parent.Name.IsNull()),
            (d => !(d.Name == null), d => !(!d.Name.IsDefined() || d.Name.IsNull())),
            (d => !(d.Name != null), d => !(d.Name.IsDefined() && !d.Name.IsNull())),
            (d => d.Name == null && d.Age != null, d => (!d.Name.IsDefined() || d.Name.IsNull()) && (d.Age.IsDefined() && !d.Age.IsNull())),
            (d => d.Children.Any(c => c.Name == null), d => d.Children.Any(c => !c.Name.IsDefined() || c.Name.IsNull()))
        ];

        foreach (var (original, expected) in cases)
        {
            string expectedSql = source.Where(expected).ToQueryDefinition().QueryText;
            IQueryable<TestDocument> rewritten = source.Where(original).WithNullSemantics();
            rewritten.ToQueryDefinition().QueryText.Should().Be(expectedSql);
            source.Where(original.WithNullSemantics()).ToQueryDefinition().QueryText.Should().Be(expectedSql);
            rewritten.WithNullSemantics().Should().BeSameAs(rewritten);
            using FeedIterator<TestDocument> iterator = rewritten.ToFeedIterator();
        }
    }

    [Test]
    public void PreservesCompositionAndExplicitTypeChecks()
    {
        using var client = new CosmosClient("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        IQueryable<TestDocument> source = client.GetContainer("test", "test").GetItemLinqQueryable<TestDocument>();
        var actual = source.Where(d => d.Name == null).OrderBy(d => d.Age).Select(d => d.Name).Take(5).WithNullSemantics();
        var expected = source.Where(d => !d.Name.IsDefined() || d.Name.IsNull()).OrderBy(d => d.Age).Select(d => d.Name).Take(5);
        actual.ToQueryDefinition().QueryText.Should().Be(expected.ToQueryDefinition().QueryText);
        var explicitQuery = source.Where(d => d.Name.IsNull() || !d.Age.IsDefined());
        explicitQuery.WithNullSemantics().Should().BeSameAs(explicitQuery);
    }

    [Test]
    public void LeavesNonPropertyAndCapturedComparisonsUnchanged()
    {
        string? captured = null;
        Expression<Func<TestDocument, bool>>[] expressions =
        [
            d => d.Name == captured,
            d => d.Name == "value",
            d => d.Name!.Trim() == null,
            d => captured == null,
            d => d.Special == null
        ];
        foreach (var expression in expressions)
            expression.WithNullSemantics().Should().BeSameAs(expression);
    }

    [Test]
    public void ExplicitRegistrationWorksAfterAnUnrecognizedComparison()
    {
        Expression<Func<TestDocument, bool>> equal = d => d.Registered == null;
        Expression<Func<TestDocument, bool>> unequal = d => d.Registered != null;
        equal.WithNullSemantics().Should().BeSameAs(equal);
        unequal.WithNullSemantics().Should().BeSameAs(unequal);

        CosmosNullSemantics.RegisterNullComparableType<RegisteredEquality>();
        CosmosNullSemantics.RegisterNullComparableType<RegisteredEquality>();

        Expression<Func<TestDocument, bool>> expectedEqual = d => !d.Registered.IsDefined() || d.Registered.IsNull();
        Expression<Func<TestDocument, bool>> expectedUnequal = d => d.Registered.IsDefined() && !d.Registered.IsNull();
        equal.WithNullSemantics().ToString().Should().Be(expectedEqual.ToString());
        unequal.WithNullSemantics().ToString().Should().Be(expectedUnequal.ToString());
    }

    [Test]
    public void RejectsNullInputs()
    {
        Action query = () => ((IQueryable<TestDocument>)null!).WithNullSemantics();
        Action predicate = () => ((Expression<Func<TestDocument, bool>>)null!).WithNullSemantics();
        query.Should().Throw<ArgumentNullException>();
        predicate.Should().Throw<ArgumentNullException>();
    }

    public sealed class TestDocument
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public TestDocument? Parent { get; set; }
        public TestDocument[] Children { get; set; } = [];
        public CustomEquality? Special { get; set; }
        public RegisteredEquality? Registered { get; set; }
    }

    public sealed class RegisteredEquality
    {
        public static bool operator ==(RegisteredEquality? left, RegisteredEquality? right) => ReferenceEquals(left, right);
        public static bool operator !=(RegisteredEquality? left, RegisteredEquality? right) => !ReferenceEquals(left, right);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => base.GetHashCode();
    }

    public sealed class CustomEquality
    {
        public static bool operator ==(CustomEquality? left, CustomEquality? right) => true;
        public static bool operator !=(CustomEquality? left, CustomEquality? right) => false;
        public override bool Equals(object? obj) => true;
        public override int GetHashCode() => 0;
    }
}
