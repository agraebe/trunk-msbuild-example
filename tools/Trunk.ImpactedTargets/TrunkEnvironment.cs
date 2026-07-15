namespace Trunk.ImpactedTargets;

/// <summary>
/// Exactly one of these is set. Trunk's docs specify the org API token for normal
/// (non-forked) PRs, and a forked-workflow-run-id instead for forked PRs, where a
/// long-lived org token can't safely be exposed to the fork's workflow run.
/// </summary>
public sealed record TrunkAuth(string? ApiToken, string? ForkedWorkflowRunId);

/// <summary>
/// Resolves the fields the Trunk API needs (see TrunkApiConstants) from either
/// Trunk-specific env vars or the GitHub Actions defaults, so the workflow doesn't
/// have to redundantly wire up values GitHub Actions already exports.
/// </summary>
public sealed class TrunkEnvironment
{
    public required TrunkAuth Auth { get; init; }
    public required RepoIdentity Repo { get; init; }
    public required PullRequestIdentity PullRequest { get; init; }
    public required string TargetBranch { get; init; }

    public static TrunkEnvironment? FromEnvironment(string baseRefFallback)
    {
        var apiToken = Environment.GetEnvironmentVariable("TRUNK_API_TOKEN");
        var forkedRunId = Environment.GetEnvironmentVariable("TRUNK_FORKED_WORKFLOW_RUN_ID");
        if (string.IsNullOrEmpty(apiToken) && string.IsNullOrEmpty(forkedRunId))
        {
            return null;
        }

        // Prefer the token when both happen to be set; forked-PR workflows should
        // only ever have the run-id available (no token secret), per Trunk's docs.
        var auth = !string.IsNullOrEmpty(apiToken)
            ? new TrunkAuth(apiToken, null)
            : new TrunkAuth(null, forkedRunId);

        var (fallbackOwner, fallbackName) = ParseGitHubRepository(
            Environment.GetEnvironmentVariable("GITHUB_REPOSITORY"));

        var owner = Environment.GetEnvironmentVariable("TRUNK_REPO_OWNER") ?? fallbackOwner;
        var name = Environment.GetEnvironmentVariable("TRUNK_REPO_NAME") ?? fallbackName;
        var host = Environment.GetEnvironmentVariable("TRUNK_REPO_HOST") ?? "github.com";

        var prNumberRaw = Environment.GetEnvironmentVariable("TRUNK_PR_NUMBER");
        var sha = Environment.GetEnvironmentVariable("TRUNK_PR_SHA")
            ?? Environment.GetEnvironmentVariable("GITHUB_SHA");
        var targetBranch = Environment.GetEnvironmentVariable("TRUNK_TARGET_BRANCH")
            ?? Environment.GetEnvironmentVariable("GITHUB_BASE_REF")
            ?? StripOriginPrefix(baseRefFallback);

        if (owner is null || name is null || sha is null ||
            !int.TryParse(prNumberRaw, out var prNumber))
        {
            return null;
        }

        return new TrunkEnvironment
        {
            Auth = auth,
            Repo = new RepoIdentity(host, owner, name),
            PullRequest = new PullRequestIdentity(prNumber, sha),
            TargetBranch = targetBranch,
        };
    }

    private static (string? Owner, string? Name) ParseGitHubRepository(string? githubRepository)
    {
        if (string.IsNullOrEmpty(githubRepository))
        {
            return (null, null);
        }

        var parts = githubRepository.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (null, null);
    }

    private static string StripOriginPrefix(string reference) =>
        reference.StartsWith("origin/", StringComparison.Ordinal) ? reference["origin/".Length..] : reference;
}
