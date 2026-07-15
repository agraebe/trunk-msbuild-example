namespace Contoso.Common.Core;

/// <summary>Small seam so downstream services don't call DateTimeOffset.UtcNow directly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
