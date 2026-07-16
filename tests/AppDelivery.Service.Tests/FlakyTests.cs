using Xunit;

namespace Contoso.AppDelivery.Service.Tests;

// ============================================================================
//  INTENTIONALLY FLAKY TEST — DEMO PURPOSES ONLY. DO NOT COPY THIS PATTERN.
//
//  Flake class: TIMING / TIGHT TIMEOUT RACE.
//
//  This races a unit of work against a timeout that is tight enough to pass
//  comfortably on an idle laptop but occasionally lose on a loaded CI runner
//  (noisy neighbor containers, cold thread-pool threads, GC pauses). This is
//  the same shape as a Playwright/Selenium test that waits N milliseconds for
//  a selector to appear: correct most of the time, flaky exactly when the
//  environment is under load. Trunk's UI groups failures like this by
//  signature (same assertion, same stack) even though the root cause is
//  "ambient system load", not the code under test.
// ============================================================================
public class FlakyTests
{
    [Fact]
    public async Task PackagePush_CompletesWithinTightTimeout()
    {
        var packagePush = SimulatePackagePushAsync();
        var timeout = Task.Delay(TimeSpan.FromMilliseconds(15));

        var completed = await Task.WhenAny(packagePush, timeout);

        Assert.True(completed == packagePush, "Package push did not complete before the tight timeout.");
    }

    private static async Task SimulatePackagePushAsync()
    {
        // Represents real work (serialize a manifest, hit a local queue, etc.)
        // that normally finishes in a few milliseconds but has no hard upper
        // bound — exactly what makes a fixed timeout here flaky under load.
        await Task.Delay(TimeSpan.FromMilliseconds(10));
    }
}
// Stress 17: touch timing flake file directly (1784238740973958000)
