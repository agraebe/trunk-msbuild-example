namespace Contoso.Identity.Service;

/// <summary>
/// Public surface consumed cross-service by AppDelivery.Service — the one deliberate
/// cross-service edge in this sample graph, so impacted-target expansion has to walk
/// more than one hop (AppDelivery depends on Identity, which depends on FeatureFlags).
/// </summary>
public interface IIdentityVerifier
{
    AuthResult Authenticate(string customerId, string username, string password);
}

public sealed record AuthResult(bool Success, string? SessionToken, bool MfaRequired);
