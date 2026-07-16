using Contoso.Common.Core;
using Contoso.Common.FeatureFlags;
using Contoso.Identity.Service;

namespace Contoso.AppDelivery.Service;

/// <summary>
/// Pushes application packages to a customer's fleet. Depends on Identity.Service to
/// verify the requesting session before allowing a rollout — the one cross-service
/// edge in the sample graph (AppDelivery -> Identity -> FeatureFlags/Core), so the
/// impacted-targets tool has to walk more than one hop of reverse edges.
/// </summary>
public sealed class AppDeliveryService
{
    private readonly IIdentityVerifier _identity;
    private readonly IFeatureFlagProvider _flags;
    private readonly List<RolloutRecord> _rollouts = new();

    public AppDeliveryService(IIdentityVerifier identity, IFeatureFlagProvider flags)
    {
        _identity = identity;
        _flags = flags;
    }

    public Result<RolloutRecord> StartRollout(string customerId, string username, string password, string appVersion)
    {
        var auth = _identity.Authenticate(customerId, username, password);
        if (!auth.Success)
        {
            return Result<RolloutRecord>.Failure("Authentication failed.");
        }

        if (auth.MfaRequired)
        {
            return Result<RolloutRecord>.Failure("MFA challenge required before rollout can start.");
        }

        var staged = _flags.IsEnabled("app-delivery-staged-rollout", customerId);
        var record = new RolloutRecord(customerId, appVersion, staged ? RolloutStrategy.Staged : RolloutStrategy.Immediate);
        _rollouts.Add(record);
        return Result<RolloutRecord>.Success(record);
    }

    public IReadOnlyList<RolloutRecord> History => _rollouts;
}

public enum RolloutStrategy
{
    Immediate,
    Staged,
}

public sealed record RolloutRecord(string CustomerId, string AppVersion, RolloutStrategy Strategy);
// Stress 09: AppDelivery.Service tweak (1784238740434424000)
