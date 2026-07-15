namespace Trunk.ImpactedTargets;

/// <summary>Either "every target" (ALL) or an explicit, closed set of target names.</summary>
public sealed record ImpactedTargetsResult(bool IsAll, IReadOnlySet<string> Targets)
{
    public static ImpactedTargetsResult All { get; } =
        new(true, new HashSet<string>());
}

/// <summary>
/// Orchestrates PathRules + ProjectGraphAnalyzer over a list of changed files to
/// produce the final impacted-target set. This class knows about repo-relative
/// paths and the custom (non-MSBuild) mapping rules; it has no idea how the changed
/// file list was obtained (git) or where the result is going (HTTP) — see
/// GitDiffProvider and TrunkApiClient for those.
/// </summary>
public sealed class ImpactedTargetsCalculator
{
    private readonly ProjectGraphAnalyzer _graphAnalyzer;
    private readonly string _repoRoot;

    public ImpactedTargetsCalculator(ProjectGraphAnalyzer graphAnalyzer, string repoRoot)
    {
        _graphAnalyzer = graphAnalyzer;
        _repoRoot = repoRoot;
    }

    public ImpactedTargetsResult Compute(IReadOnlyList<string> changedFiles)
    {
        var directlyChangedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in changedFiles)
        {
            // Rule: build infrastructure changes conservatively impact everything.
            // See PathRules.BuildInfrastructurePaths for why this is deliberate,
            // not laziness — an empty/wrong answer under-tests the PR.
            if (PathRules.IsBuildInfrastructurePath(file))
            {
                return ImpactedTargetsResult.All;
            }

            // Rule: seed data isn't a ProjectReference edge, so encode it explicitly.
            if (PathRules.IsSeedDataPath(file))
            {
                foreach (var root in PathRules.SeedDataDirectRoots)
                {
                    directlyChangedProjects.Add(root);
                }

                continue;
            }

            var owningProject = _graphAnalyzer.FindOwningProject(_repoRoot, file);
            if (owningProject is not null)
            {
                directlyChangedProjects.Add(owningProject);
            }

            // A file matching no project and no custom rule (README.md, LICENSE,
            // etc.) contributes nothing to the impact set. That's correct: it's not
            // silently dropped, it's explicitly out of scope.
        }

        var expanded = _graphAnalyzer.ExpandToDependents(directlyChangedProjects);
        return new ImpactedTargetsResult(false, expanded);
    }
}
