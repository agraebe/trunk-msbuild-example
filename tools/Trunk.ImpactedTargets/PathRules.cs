namespace Trunk.ImpactedTargets;

/// <summary>
/// Applies a <see cref="PathRulesConfig"/> to repo-relative file paths. This class
/// is pure engine — it has no idea what "Common.Core" or "data/seed/" mean, it just
/// evaluates whatever rules the config hands it. That's deliberate: this is the
/// class you keep unmodified when reusing this tool in a different repo. All the
/// repo-specific knowledge lives in the config file this loads (see
/// PathRulesConfig.cs and the README in this directory).
///
/// Keep the config short and explicit. Resist the urge to make matching "smart"
/// (e.g. pattern-matching on file content) — a wrong guess here silently
/// under-tests a PR, which is worse than a human maintaining an explicit list.
/// </summary>
public sealed class PathRules
{
    private readonly PathRulesConfig _config;

    public PathRules(PathRulesConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Returns the project names directly impacted by a change at
    /// <paramref name="repoRelativePath"/> via path-based rules (e.g. seed data),
    /// or an empty sequence if no rule matches. Does not expand to dependents —
    /// that's <see cref="ProjectGraphAnalyzer.ExpandToDependents"/>'s job.
    /// </summary>
    public IEnumerable<string> GetDirectlyImpactedProjects(string repoRelativePath)
    {
        var normalized = Normalize(repoRelativePath);

        foreach (var rule in _config.PathBasedRules)
        {
            if (normalized.StartsWith(Normalize(rule.PathPrefix), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var project in rule.ImpactedProjects)
                {
                    yield return project;
                }
            }
        }
    }

    public bool IsBuildInfrastructurePath(string repoRelativePath)
    {
        var normalized = Normalize(repoRelativePath);

        foreach (var marker in _config.BuildInfrastructurePaths)
        {
            if (marker.EndsWith('/'))
            {
                if (normalized.StartsWith(Normalize(marker), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (normalized.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
