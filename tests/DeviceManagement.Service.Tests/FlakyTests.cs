using Xunit;

namespace Contoso.DeviceManagement.Service.Tests;

// ============================================================================
//  INTENTIONALLY FLAKY TEST — DEMO PURPOSES ONLY. DO NOT COPY THIS PATTERN.
//
//  Flake class: RANDOM FAILURE.
//
//  This simulates a test that depends on non-deterministic runtime behavior
//  (race with a background thread, hash-ordering, a randomized retry/backoff
//  path, etc.) without a fixed seed, so its failure rate is a genuine
//  probability rather than a coin flip that's secretly deterministic per
//  build. It fails roughly 25% of the time. Do not "fix" this by fixing the
//  Guid/random seed — that would defeat the point of the demo, which is to
//  give Trunk's flaky-test detector something with a real, unpredictable
//  failure signature to find across CI runs.
// ============================================================================
public class FlakyTests
{
    [Fact]
    public void DeviceHeartbeat_ProcessesWithinBudget()
    {
        // Guid.NewGuid() is not seeded, so this genuinely varies run to run —
        // unlike `new Random(fixedSeed)`, which would be deterministic forever.
        var roll = Guid.NewGuid().GetHashCode();
        var normalized = (uint)roll % 100;

        // ~25% chance of "missing the budget", mimicking an occasional slow
        // heartbeat under load that isn't reproducible from the seed alone.
        Assert.True(normalized >= 25, $"Simulated heartbeat missed its budget (roll={normalized}).");
    }
}
