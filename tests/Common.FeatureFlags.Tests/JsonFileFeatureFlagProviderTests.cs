using Contoso.Common.FeatureFlags;
using Xunit;

namespace Contoso.Common.FeatureFlags.Tests;

public class JsonFileFeatureFlagProviderTests
{
    private readonly JsonFileFeatureFlagProvider _provider = new();

    [Fact]
    public void IsEnabled_GloballyEnabledFlag_ReturnsTrueForAnyCustomer()
    {
        Assert.True(_provider.IsEnabled("new-device-onboarding-flow"));
        Assert.True(_provider.IsEnabled("new-device-onboarding-flow", "some-random-customer"));
    }

    [Fact]
    public void IsEnabled_UnknownFlag_DefaultsToFalse()
    {
        Assert.False(_provider.IsEnabled("does-not-exist"));
    }

    [Fact]
    public void IsEnabled_PerCustomerFlag_TrueForListedCustomer()
    {
        Assert.True(_provider.IsEnabled("identity-mfa-required", "cust-001"));
    }

    [Fact]
    public void IsEnabled_PerCustomerFlag_FalseForUnlistedCustomer()
    {
        Assert.False(_provider.IsEnabled("identity-mfa-required", "cust-002"));
    }

    [Fact]
    public void IsEnabled_PerCustomerFlag_FalseWhenNoCustomerIdGiven()
    {
        Assert.False(_provider.IsEnabled("identity-mfa-required"));
    }

    [Fact]
    public void Constructor_MissingFile_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "flags.json");
        Assert.Throws<FileNotFoundException>(() => new JsonFileFeatureFlagProvider(missingPath));
    }

    [Fact]
    public void IsEnabled_StagedRolloutFlag_EnabledGlobally()
    {
        Assert.True(_provider.IsEnabled("app-delivery-staged-rollout"));
    }
}
