namespace Trunk.ImpactedTargets;

/// <summary>
/// Hand-written rules for mapping repo paths to impacted targets that MSBuild's
/// project graph cannot see, because they aren't .csproj/ProjectReference edges at
/// all. This is the piece a real adoption has to customize the most — every
/// enterprise monorepo has a handful of "everything reads this" files that live
/// outside the build graph (seed data, shared config, generated schemas, etc.).
///
/// Keep this list short and explicit. Resist the urge to make it "smart" (e.g.
/// pattern-matching on file content) — a wrong guess here silently under-tests a
/// PR, which is worse than a human maintaining an explicit list.
/// </summary>
public static class PathRules
{
    /// <summary>
    /// Rule 1 — seed data. data/seed/*.json is read at runtime by
    /// Common.Core.SeedDataLoader, so any project that transitively depends on
    /// Common.Core is potentially affected by a seed-data change, even though no
    /// .csproj references the JSON files. We approximate "depends on Common.Core"
    /// by hard-coding the known seed consumers here; the graph analyzer expands
    /// each one to its full dependent closure exactly as it would for a normal
    /// project change.
    /// </summary>
    public static readonly string SeedDataDirectory = "data/seed/";

    public static readonly IReadOnlyList<string> SeedDataDirectRoots = new[]
    {
        "Common.Core",
    };

    /// <summary>
    /// Rule 2 — build infrastructure. Changes to these paths can alter how *every*
    /// project builds or tests, so we conservatively mark the whole repo impacted
    /// rather than trying to reason about which projects are "really" affected.
    /// An empty/wrong impact set would let the merge queue under-test a PR that
    /// changes shared build behavior — a loud, maximal answer is the safe default.
    /// </summary>
    public static readonly IReadOnlyList<string> BuildInfrastructurePaths = new[]
    {
        "Directory.Build.props",
        "Directory.Build.targets",
        "global.json",
        "NuGet.config",
        ".sln",
        ".github/workflows/",
        "tools/Trunk.ImpactedTargets/",
    };

    public static bool IsSeedDataPath(string repoRelativePath) =>
        repoRelativePath.Replace('\\', '/').StartsWith(SeedDataDirectory, StringComparison.OrdinalIgnoreCase);

    public static bool IsBuildInfrastructurePath(string repoRelativePath)
    {
        var normalized = repoRelativePath.Replace('\\', '/');
        foreach (var marker in BuildInfrastructurePaths)
        {
            if (marker.EndsWith('/'))
            {
                if (normalized.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
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
}
