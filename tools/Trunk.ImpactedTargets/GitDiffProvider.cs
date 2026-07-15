using System.Diagnostics;

namespace Trunk.ImpactedTargets;

/// <summary>Shells out to `git diff --name-only` to enumerate changed files between two refs.</summary>
public sealed class GitDiffProvider
{
    private readonly string _repoRoot;

    public GitDiffProvider(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    /// <summary>
    /// Returns repo-relative paths changed between the merge-base of <paramref name="baseRef"/>
    /// and <paramref name="headRef"/>. Uses the three-dot "merge-base" form (base..head is
    /// two-dot / direct diff; base...head diffs against the common ancestor) so a PR branch
    /// that is behind main doesn't pick up unrelated changes already on main.
    /// </summary>
    public IReadOnlyList<string> GetChangedFiles(string baseRef, string headRef)
    {
        var (exitCode, stdout, stderr) = RunGit($"diff --name-only {baseRef}...{headRef}");
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"git diff failed (exit {exitCode}) for '{baseRef}...{headRef}': {stderr}");
        }

        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private (int ExitCode, string StdOut, string StdErr) RunGit(string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
