using Soenneker.Gen.EnumValues;

namespace Soenneker.Cosmos.Linq.Tests;

[EnumValue<string>]
public sealed partial class DeliverySource
{
    public static readonly DeliverySource Manual = new("manual");
    public static readonly DeliverySource Automation = new("automation");
}

[EnumValue]
public sealed partial class DeliveryStatus
{
    public static readonly DeliveryStatus Pending = new(0);
    public static readonly DeliveryStatus Sent = new(1);
}
