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
    private readonly string _apiToken;

    public TrunkApiClient(HttpClient httpClient, string apiToken)
    {
        _httpClient = httpClient;
        _apiToken = apiToken;
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
            new PullRequestPayload(pullRequest.Number, pullRequest.Sha),
            targetBranch,
            result.IsAll ? "ALL" : result.Targets.OrderBy(t => t, StringComparer.Ordinal).ToArray());

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, TrunkApiConstants.SetImpactedTargetsUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(TrunkApiConstants.ApiTokenHeader, _apiToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Trunk API returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
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

    private sealed record PullRequestPayload(int Number, string Sha);
}
