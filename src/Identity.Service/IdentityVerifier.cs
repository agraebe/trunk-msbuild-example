using Contoso.Common.Core;
using Contoso.Common.FeatureFlags;

namespace Contoso.Identity.Service;

public sealed class IdentityVerifier : IIdentityVerifier
{
    private readonly IFeatureFlagProvider _flags;
    private readonly IClock _clock;
    private readonly Dictionary<string, string> _passwordsByUsername;

    public IdentityVerifier(IFeatureFlagProvider flags, IClock clock, Dictionary<string, string>? knownUsers = null)
    {
        _flags = flags;
        _clock = clock;
        _passwordsByUsername = knownUsers ?? new Dictionary<string, string>
        {
            ["alice"] = "correct-horse-battery-staple",
            ["bob"] = "hunter2",
        };
    }

    public AuthResult Authenticate(string customerId, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, null, false);
        }

        var known = _passwordsByUsername.TryGetValue(username, out var expected);
        if (!known || expected != password)
        {
            return new AuthResult(false, null, false);
        }

        var mfaRequired = _flags.IsEnabled("identity-mfa-required", customerId);
        if (mfaRequired)
        {
            // Caller must complete a second factor before a session token is issued.
            return new AuthResult(true, null, MfaRequired: true);
        }

        var token = $"{username}.{_clock.UtcNow.ToUnixTimeSeconds()}";
        return new AuthResult(true, token, MfaRequired: false);
    }
}
// Stress 05: Identity.Service tweak (1784238740172018000)
