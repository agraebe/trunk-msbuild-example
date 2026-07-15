using Contoso.Common.Core;
using Contoso.Common.FeatureFlags;
using Contoso.Identity.Service;
using Xunit;

namespace Contoso.Identity.Service.Tests;

public class IdentityVerifierTests
{
    private static IdentityVerifier CreateVerifier() =>
        new(new JsonFileFeatureFlagProvider(), new SystemClock());

    [Fact]
    public void Authenticate_ValidCredentials_Succeeds()
    {
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-002", "alice", "correct-horse-battery-staple");
        Assert.True(result.Success);
    }

    [Fact]
    public void Authenticate_WrongPassword_Fails()
    {
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-002", "alice", "wrong-password");
        Assert.False(result.Success);
    }

    [Fact]
    public void Authenticate_UnknownUsername_Fails()
    {
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-002", "nobody", "whatever");
        Assert.False(result.Success);
    }

    [Fact]
    public void Authenticate_EmptyPassword_Fails()
    {
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-002", "alice", "");
        Assert.False(result.Success);
    }

    [Fact]
    public void Authenticate_MfaRequiredCustomer_ReturnsSuccessWithoutToken()
    {
        // cust-001 has identity-mfa-required enabled in flags.json.
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-001", "alice", "correct-horse-battery-staple");

        Assert.True(result.Success);
        Assert.True(result.MfaRequired);
        Assert.Null(result.SessionToken);
    }

    [Fact]
    public void Authenticate_NonMfaCustomer_ReturnsSessionToken()
    {
        var verifier = CreateVerifier();
        var result = verifier.Authenticate("cust-002", "bob", "hunter2");

        Assert.True(result.Success);
        Assert.False(result.MfaRequired);
        Assert.NotNull(result.SessionToken);
    }

    [Fact]
    public void SessionStore_TrackedToken_IsActiveWithinTtl()
    {
        var store = new SessionStore(new SystemClock());
        store.Track("token-abc");
        Assert.True(store.IsActive("token-abc", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void SessionStore_UnknownToken_IsNotActive()
    {
        var store = new SessionStore(new SystemClock());
        Assert.False(store.IsActive("never-issued", TimeSpan.FromMinutes(5)));
    }
}
