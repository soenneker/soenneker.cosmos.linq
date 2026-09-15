using System;

namespace Soenneker.Cosmos.Linq.Tests;

public sealed class ApplicationDocument
{
    public string? Name { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTime? Date { get; set; }
    public Guid? Identifier { get; set; }
    public decimal? Amount { get; set; }
    public DeliverySource? Source { get; set; }
    public ApplicationDocument? Parent { get; set; }
    public ApplicationDocument[]? Children { get; set; }
    public string[]? Tags { get; set; }
}
