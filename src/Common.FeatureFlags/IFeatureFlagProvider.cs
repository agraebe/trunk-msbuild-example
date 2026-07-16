namespace Contoso.Common.FeatureFlags;

/// <summary>
/// Contoso's home-grown feature-flag interface. Every service in this monorepo depends
/// on Common.FeatureFlags directly, which is the point of this sample: it is exactly
/// the kind of small, widely-shared library that turns into a merge-queue bottleneck.
/// A PR that touches this project has to be treated as impacting every one of its
/// dependents, because any one of them might call IsEnabled() differently afterward.
/// </summary>
public interface IFeatureFlagProvider
{
    bool IsEnabled(string flagName, string? customerId = null);
}
// Stress 04: FeatureFlags tweak (big lane) (1784238740103505000)
