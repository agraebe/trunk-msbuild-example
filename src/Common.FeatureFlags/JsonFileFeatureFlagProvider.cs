using System.Text.Json;

namespace Contoso.Common.FeatureFlags;

/// <summary>
/// In-memory feature-flag provider backed by a flat JSON file. This is deliberately
/// simple (no polling, no remote config service) to mirror the kind of homegrown
/// flagging system a large enterprise monorepo tends to accumulate before adopting
/// something like LaunchDarkly. The whole point in this demo is the *dependency
/// fan-out*, not the flag evaluation logic itself.
/// </summary>
public sealed class JsonFileFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly IReadOnlyDictionary<string, FlagDefinition> _flags;

    public JsonFileFeatureFlagProvider(string? flagsFilePath = null)
    {
        var path = flagsFilePath ?? Path.Combine(AppContext.BaseDirectory, "flags.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Feature flag file not found at '{path}'.", path);
        }

        using var stream = File.OpenRead(path);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, FlagDefinition>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _flags = parsed ?? new Dictionary<string, FlagDefinition>();
    }

    public bool IsEnabled(string flagName, string? customerId = null)
    {
        if (!_flags.TryGetValue(flagName, out var definition))
        {
            // Unknown flags default closed. Fail safe, not loud, in production code.
            return false;
        }

        if (definition.Enabled)
        {
            return true;
        }

        return customerId is not null && definition.EnabledForCustomerIds.Contains(customerId);
    }
}
// demo PR v2: touch Common.FeatureFlags for impacted-targets.yml (money shot)
