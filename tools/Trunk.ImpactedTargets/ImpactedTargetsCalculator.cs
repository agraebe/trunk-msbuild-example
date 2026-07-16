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
    private readonly PathRules _pathRules;
    private readonly string _repoRoot;

    public ImpactedTargetsCalculator(ProjectGraphAnalyzer graphAnalyzer, PathRules pathRules, string repoRoot)
    {
        _graphAnalyzer = graphAnalyzer;
        _pathRules = pathRules;
        _repoRoot = repoRoot;
    }

    public ImpactedTargetsResult Compute(IReadOnlyList<string> changedFiles)
    {
        var directlyChangedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in changedFiles)
        {
            // Rule: build infrastructure changes conservatively impact everything.
            // See the config's buildInfrastructurePaths for why this is deliberate,
            // not laziness — an empty/wrong answer under-tests the PR.
            if (_pathRules.IsBuildInfrastructurePath(file))
            {
                return ImpactedTargetsResult.All;
            }

            // Rule: path-based rules cover repo artifacts that affect projects
            // without being a ProjectReference edge (seed data, shared config, etc.)
            var directHits = _pathRules.GetDirectlyImpactedProjects(file).ToList();
            if (directHits.Count > 0)
            {
                foreach (var project in directHits)
                {
                    directlyChangedProjects.Add(project);
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
