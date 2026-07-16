using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trunk.ImpactedTargets;

/// <summary>
/// The one file that makes this tool portable across repos: everything
/// repo-specific lives in data (this config), not in the engine code
/// (<see cref="PathRules"/>, <see cref="ProjectGraphAnalyzer"/>,
/// <see cref="ImpactedTargetsCalculator"/>). Adapting this tool to a real
/// monorepo should mean editing/replacing the JSON file this loads, never
/// touching a .cs file. See README.md in this directory.
/// </summary>
public sealed class PathRulesConfig
{
    /// <summary>
    /// Path-prefix rules for repo artifacts that affect projects without being a
    /// ProjectReference edge MSBuild's graph can see — seed data, shared config,
    /// generated schemas, etc. Each rule maps "changed files under this prefix"
    /// to "treat these project names as directly changed"; the graph analyzer
    /// then expands each one to its normal transitive-dependent closure.
    /// </summary>
    [JsonPropertyName("pathBasedRules")]
    public List<PathBasedRule> PathBasedRules { get; set; } = new();

    /// <summary>
    /// Path markers for build infrastructure. A change under any of these is
    /// conservatively treated as impacting every target — see PathRules.cs for
    /// why a loud "everything" beats a wrong "nothing".
    /// </summary>
    [JsonPropertyName("buildInfrastructurePaths")]
    public List<string> BuildInfrastructurePaths { get; set; } = new();

    /// <summary>
    /// Loads config from <paramref name="path"/>. Throws if the file is missing
    /// or malformed — same "fail loud, don't silently under-test" reasoning as
    /// the rest of this tool: a missing config could otherwise be mistaken for
    /// "no repo-specific rules apply" when really nobody wrote one yet.
    /// </summary>
    public static PathRulesConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Impacted-targets config not found at '{path}'. This tool has no built-in " +
                "knowledge of your repo's non-MSBuild dependencies (seed data, shared config, " +
                "build infrastructure) — see README.md in this directory for the config schema " +
                "and a worked example.",
                path);
        }

        using var stream = File.OpenRead(path);
        var config = JsonSerializer.Deserialize<PathRulesConfig>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return config ?? new PathRulesConfig();
    }
}

public sealed class PathBasedRule
{
    /// <summary>Repo-relative path prefix, e.g. "data/seed/". Forward slashes; matched case-insensitively.</summary>
    [JsonPropertyName("pathPrefix")]
    public string PathPrefix { get; set; } = "";

    /// <summary>MSBuild project names to treat as directly changed when a file under PathPrefix changes.</summary>
    [JsonPropertyName("impactedProjects")]
    public List<string> ImpactedProjects { get; set; } = new();
}
