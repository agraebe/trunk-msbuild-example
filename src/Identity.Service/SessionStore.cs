using Contoso.Common.Core;

namespace Contoso.Identity.Service;

/// <summary>In-memory session tracking; a real service would back this with a cache/DB.</summary>
public sealed class SessionStore
{
    private readonly IClock _clock;
    private readonly Dictionary<string, DateTimeOffset> _issuedAt = new();

    public SessionStore(IClock clock)
    {
        _clock = clock;
    }

    public void Track(string sessionToken)
    {
        _issuedAt[sessionToken] = _clock.UtcNow;
    }

    public bool IsActive(string sessionToken, TimeSpan ttl)
    {
        if (!_issuedAt.TryGetValue(sessionToken, out var issuedAt))
        {
            return false;
        }

        return _clock.UtcNow - issuedAt < ttl;
    }
}
