namespace Trunk.ImpactedTargets.Tests;

/// <summary>
/// These tests exercise ProjectGraphAnalyzer against the *real* solution in this
/// repo rather than a synthetic fixture — the monorepo under src/ and tests/ IS the
/// test fixture for the impacted-targets tool. This locates the repo root by
/// walking up from the test assembly's output directory until it finds the .sln.
/// </summary>
internal static class RepoRootLocator
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("*.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find a .sln file walking up from {AppContext.BaseDirectory}.");
    }

    public static string FindSolutionFile() =>
        Directory.GetFiles(Find(), "*.sln", SearchOption.TopDirectoryOnly).Single();
}
