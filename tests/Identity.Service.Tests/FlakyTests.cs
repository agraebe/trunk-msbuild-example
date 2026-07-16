using Xunit;

namespace Contoso.Identity.Service.Tests;

// ============================================================================
//  INTENTIONALLY FLAKY TEST — DEMO PURPOSES ONLY. DO NOT COPY THIS PATTERN.
//
//  Flake class: SHARED MUTABLE STATE / RACE CONDITION.
//
//  FirstTenantOnboardingFlakyTests and SecondTenantOnboardingFlakyTests each read
//  and increment the same static counter, and each assumes it is the first caller.
//  They are two separate test *classes* deliberately: xUnit treats each class with
//  no explicit [Collection] attribute as its own implicit collection, and runs
//  different collections concurrently by default. That means these two tests
//  genuinely race on a shared, unsynchronized static field — which one "wins" and
//  sees the counter at 1 depends on real OS thread-scheduling, not build order. Add
//  the small random delay before touching the counter and the interleaving varies
//  run to run: sometimes both assertions pass, sometimes one loses the race and
//  fails. That's the textbook shape of a shared-static-state flake in a monorepo
//  full of singletons and static caches — and exactly what re-running a failed
//  test alone would NOT reproduce, because in isolation there's no race to lose.
// ============================================================================
public class FirstTenantOnboardingFlakyTests
{
    [Fact]
    public async Task FirstTenantOnboarding_ReceivesSessionIdOne()
    {
        SessionIdGenerator.ArriveAndWaitForRace();
        await Task.Run(() => Thread.Sleep(Random.Shared.Next(0, 15)));
        var id = SessionIdGenerator.Next();
        Assert.Equal(1, id);
    }
}

public class SecondTenantOnboardingFlakyTests
{
    [Fact]
    public async Task SecondTenantOnboarding_AlsoExpectsSessionIdOne()
    {
        // Bug: written assuming it always runs first against a fresh counter. It
        // shares static state with FirstTenantOnboardingFlakyTests above, and
        // xUnit runs the two test classes concurrently by default (see banner).
        SessionIdGenerator.ArriveAndWaitForRace();
        await Task.Run(() => Thread.Sleep(Random.Shared.Next(0, 15)));
        var id = SessionIdGenerator.Next();
        Assert.Equal(1, id);
    }
}

/// <summary>Static, process-wide counter — the source of the shared state above.</summary>
public static class SessionIdGenerator
{
    private static int _current;

    // Lines both "concurrent onboarding requests" up so they hit Next() at
    // genuinely unpredictable relative times instead of whichever test class
    // xUnit happens to dispatch first. A 2-second timeout means a caller that
    // runs alone (e.g. `dotnet test --filter`) doesn't hang forever waiting for
    // a partner that will never arrive.
    private static readonly Barrier RaceGate = new(participantCount: 2);

    public static void ArriveAndWaitForRace()
    {
        try
        {
            RaceGate.SignalAndWait(TimeSpan.FromSeconds(2));
        }
        catch (ObjectDisposedException)
        {
            // Barrier already used up by a prior test run in this process.
        }
    }

    public static int Next() => Interlocked.Increment(ref _current);
}
// Stress 15: touch shared-state flake file directly (1784238740841731000)
