using Soenneker.Gen.EnumValues;

namespace Soenneker.Cosmos.Linq.Tests;

[EnumValue<string>]
public sealed partial class DeliverySource
{
    public static readonly DeliverySource Manual = new("manual");
    public static readonly DeliverySource Automation = new("automation");
}
