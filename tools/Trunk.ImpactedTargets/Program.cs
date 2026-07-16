using System.Text.Json;
using Trunk.ImpactedTargets;

// MSBuildLocator must run before any other statement touches a Microsoft.Build.*
// type — including indirectly, via the JIT resolving a method signature that
// mentions one. Keep this call first, full stop. See MsBuildRegistration.cs for why.
MsBuildRegistration.EnsureRegistered();

var options = CliOptions.Parse(args);
if (options is null)
{
    CliOptions.PrintUsage();
    return 2;
}

try
{
    var repoRoot = Path.GetFullPath(options.RepoRoot);
    var solutionPath = FindSolutionFile(repoRoot);

    Console.Error.WriteLine($"Loading MSBuild project graph from {solutionPath}...");
    var graphAnalyzer = new ProjectGraphAnalyzer(solutionPath);

    Console.Error.WriteLine($"Loading path-rules config from {options.ConfigPath}...");
    var pathRules = new PathRules(PathRulesConfig.Load(options.ConfigPath));

    var gitDiff = new GitDiffProvider(repoRoot);
    var changedFiles = gitDiff.GetChangedFiles(options.BaseRef, options.HeadRef);
    Console.Error.WriteLine($"{changedFiles.Count} file(s) changed between {options.BaseRef} and {options.HeadRef}.");

    var calculator = new ImpactedTargetsCalculator(graphAnalyzer, pathRules, repoRoot);
    var result = calculator.Compute(changedFiles);

    object jsonPayload = result.IsAll
        ? "ALL"
        : result.Targets.OrderBy(t => t, StringComparer.Ordinal).ToArray();

    var json = JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);

    if (options.DryRun)
    {
        Console.Error.WriteLine("--dry-run set: skipping POST to Trunk.");
        return 0;
    }

    var env = TrunkEnvironment.FromEnvironment(options.BaseRef);
    if (env is null)
    {
        Console.Error.WriteLine(
            "Missing required Trunk/GitHub environment variables for a live POST. " +
            "Re-run with --dry-run, or set TRUNK_API_TOKEN (or TRUNK_FORKED_WORKFLOW_RUN_ID " +
            "for forked PRs), TRUNK_REPO_OWNER/TRUNK_REPO_NAME (or GITHUB_REPOSITORY), " +
            "TRUNK_PR_NUMBER, TRUNK_PR_SHA (or GITHUB_SHA), and " +
            "TRUNK_TARGET_BRANCH (or GITHUB_BASE_REF).");
        return 1;
    }

    using var httpClient = new HttpClient();
    var apiClient = new TrunkApiClient(httpClient, env.Auth);
    await apiClient.PostImpactedTargetsAsync(env.Repo, env.PullRequest, env.TargetBranch, result, CancellationToken.None);
    Console.Error.WriteLine("Posted impacted targets to Trunk.");

    return 0;
}
catch (Exception ex)
{
    // Deliberately fail loud and non-zero on ANY error computing the graph or the
    // diff. A crash that stops the merge queue is recoverable (retry, alert,
    // fall back to "test everything" manually); a silently wrong *empty* impact
    // set is not — it would let the queue under-test a PR and ship a break. See
    // the Part 3 requirement in the README for the same reasoning.
    Console.Error.WriteLine($"trunk-impacted-targets failed: {ex}");
    return 1;
}

static string FindSolutionFile(string repoRoot)
{
    var solutions = Directory.GetFiles(repoRoot, "*.sln", SearchOption.TopDirectoryOnly);
    if (solutions.Length == 1)
    {
        return solutions[0];
    }

    throw new FileNotFoundException(
        $"Expected exactly one .sln file at repo root '{repoRoot}', found {solutions.Length}.");
}
