namespace Contoso.Common.FeatureFlags;

public sealed class FlagDefinition
{
    public bool Enabled { get; set; }

    public List<string> EnabledForCustomerIds { get; set; } = new();
}
