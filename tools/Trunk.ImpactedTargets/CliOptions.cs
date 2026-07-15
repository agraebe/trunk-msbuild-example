namespace Trunk.ImpactedTargets;

public sealed class CliOptions
{
    public required string BaseRef { get; init; }
    public required string HeadRef { get; init; }
    public required string RepoRoot { get; init; }
    public required bool DryRun { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        string? baseRef = null;
        var headRef = "HEAD";
        var repoRoot = Directory.GetCurrentDirectory();
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--base":
                    baseRef = RequireValue(args, ref i, "--base");
                    break;
                case "--head":
                    headRef = RequireValue(args, ref i, "--head");
                    break;
                case "--repo-root":
                    repoRoot = RequireValue(args, ref i, "--repo-root");
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "-h":
                case "--help":
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }

        if (baseRef is null)
        {
            Console.Error.WriteLine("--base <ref> is required (e.g. --base origin/main).");
            return null;
        }

        return new CliOptions
        {
            BaseRef = baseRef,
            HeadRef = headRef,
            RepoRoot = repoRoot,
            DryRun = dryRun,
        };
    }

    private static string RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"{flag} requires a value.");
        }

        i++;
        return args[i];
    }

    public static void PrintUsage()
    {
        Console.Error.WriteLine("""
            Usage: trunk-impacted-targets --base <ref> [--head <ref>] [--repo-root <path>] [--dry-run]

              --base <ref>       Merge-base target, e.g. origin/main. Required.
              --head <ref>       Ref to diff against --base. Defaults to HEAD.
              --repo-root <path> Repository root. Defaults to the current directory.
              --dry-run          Print the impacted-targets JSON but skip the POST to Trunk.

            Environment variables (required unless --dry-run):
              TRUNK_API_TOKEN     Trunk organization API token. Use this OR the
                                  forked-PR variable below, not both.
              TRUNK_FORKED_WORKFLOW_RUN_ID
                                  Forked-PR alternative to TRUNK_API_TOKEN, per
                                  Trunk's docs (a fork's workflow run can't hold
                                  an org secret). Typically ${{ github.run_id }}.
              TRUNK_REPO_OWNER    GitHub repo owner. Falls back to parsing GITHUB_REPOSITORY.
              TRUNK_REPO_NAME     GitHub repo name.  Falls back to parsing GITHUB_REPOSITORY.
              TRUNK_REPO_HOST     Defaults to "github.com".
              TRUNK_PR_NUMBER     Pull request number.
              TRUNK_PR_SHA        Head commit SHA. Falls back to GITHUB_SHA.
              TRUNK_TARGET_BRANCH Merge target branch. Falls back to GITHUB_BASE_REF, then --base.
            """);
    }
}
