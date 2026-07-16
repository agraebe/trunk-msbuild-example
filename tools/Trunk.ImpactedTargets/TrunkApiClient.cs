using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trunk.ImpactedTargets;

public sealed record RepoIdentity(string Host, string Owner, string Name);

public sealed record PullRequestIdentity(int Number, string Sha);

/// <summary>POSTs the computed impacted targets to Trunk's Merge Queue API.</summary>
public sealed class TrunkApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TrunkAuth _auth;

    public TrunkApiClient(HttpClient httpClient, TrunkAuth auth)
    {
        _httpClient = httpClient;
        _auth = auth;
    }

    public async Task PostImpactedTargetsAsync(
        RepoIdentity repo,
        PullRequestIdentity pullRequest,
        string targetBranch,
        ImpactedTargetsResult result,
        CancellationToken cancellationToken)
    {
        var payload = new SetImpactedTargetsRequest(
            new RepoPayload(repo.Host, repo.Owner, repo.Name),
            // Trunk's own reference client (trunk-io/bazel-action, upload_impacted_targets.sh)
            // builds this field with `jq --arg number "${PR_NUMBER}"`, which always emits a JSON
            // string. Sending a JSON integer here is accepted by the write endpoint (200 OK) but
            // apparently doesn't key-match the PR record the merge-queue readiness engine already
            // has from the GitHub webhook — send it as a string to match the known-working client.
            new PullRequestPayload(pullRequest.Number.ToString(), pullRequest.Sha),
            targetBranch,
            result.IsAll ? "ALL" : result.Targets.OrderBy(t => t, StringComparer.Ordinal).ToArray());

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, TrunkApiConstants.SetImpactedTargetsUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (_auth.ApiToken is not null)
        {
            request.Headers.Add(TrunkApiConstants.ApiTokenHeader, _auth.ApiToken);
        }
        else if (_auth.ForkedWorkflowRunId is not null)
        {
            request.Headers.Add(TrunkApiConstants.ForkedWorkflowRunIdHeader, _auth.ForkedWorkflowRunId);
        }
        else
        {
            throw new InvalidOperationException("TrunkAuth has neither an API token nor a forked-workflow-run-id.");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Trunk API returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        // Trunk returns 2xx even when it accepts the payload but can't fully act on
        // it (e.g. a repo/org mismatch it silently no-ops on) — log the body so a
        // "why isn't this showing up in the queue" investigation doesn't have to
        // guess blind from a bare success exit code.
        Console.Error.WriteLine($"Trunk responded {(int)response.StatusCode}: {body}");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Payload shape mirrors https://docs.trunk.io/merge-queue/optimizations/parallel-queues/api
    // exactly: { repo: { host, owner, name }, pr: { number, sha }, targetBranch, impactedTargets }.
    // impactedTargets is a string array OR the literal string "ALL" — System.Text.Json
    // serializes `object` fine for either shape here since we hand it a string[] or string.
    private sealed record SetImpactedTargetsRequest(
        RepoPayload Repo,
        [property: JsonPropertyName("pr")] PullRequestPayload PullRequest,
        string TargetBranch,
        object ImpactedTargets);

    private sealed record RepoPayload(string Host, string Owner, string Name);

    private sealed record PullRequestPayload(string Number, string Sha);
}
