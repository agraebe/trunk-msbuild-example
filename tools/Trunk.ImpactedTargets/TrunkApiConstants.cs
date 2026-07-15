namespace Trunk.ImpactedTargets;

/// <summary>
/// The Trunk Merge Queue "impacted targets" API contract, as documented at
/// https://docs.trunk.io/merge-queue/optimizations/parallel-queues/api (fetched and
/// verified while building this example — not guessed from memory). If Trunk changes
/// this contract, this is the one file to update; nothing else in the tool encodes
/// endpoint/header/payload details.
/// </summary>
public static class TrunkApiConstants
{
    /// <summary>POST https://api.trunk.io:443/v1/setImpactedTargets</summary>
    public const string SetImpactedTargetsUrl = "https://api.trunk.io:443/v1/setImpactedTargets";

    /// <summary>Org API token header, used for non-forked-PR workflow runs.</summary>
    public const string ApiTokenHeader = "x-api-token";

    /// <summary>Used instead of an API token for forked-repo PRs, per Trunk's docs.</summary>
    public const string ForkedWorkflowRunIdHeader = "x-forked-workflow-run-id";
}
