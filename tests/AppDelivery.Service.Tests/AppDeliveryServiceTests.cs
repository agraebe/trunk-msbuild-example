using Contoso.AppDelivery.Service;
using Contoso.Common.FeatureFlags;
using Contoso.Identity.Service;
using Xunit;

namespace Contoso.AppDelivery.Service.Tests;

public class AppDeliveryServiceTests
{
    private static AppDeliveryService CreateService() =>
        new(new IdentityVerifier(new JsonFileFeatureFlagProvider(), new Contoso.Common.Core.SystemClock()),
            new JsonFileFeatureFlagProvider());

    [Fact]
    public void StartRollout_ValidCredentials_Succeeds()
    {
        var service = CreateService();
        var result = service.StartRollout("cust-002", "bob", "hunter2", "1.4.0");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void StartRollout_InvalidCredentials_Fails()
    {
        var service = CreateService();
        var result = service.StartRollout("cust-002", "bob", "wrong", "1.4.0");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void StartRollout_MfaCustomer_Fails()
    {
        // cust-001 requires MFA per flags.json; StartRollout doesn't handle the
        // second factor, so it should surface as a failure rather than proceed.
        var service = CreateService();
        var result = service.StartRollout("cust-001", "alice", "correct-horse-battery-staple", "1.4.0");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void StartRollout_StagedFlagEnabled_UsesStagedStrategy()
    {
        var service = CreateService();
        var result = service.StartRollout("cust-002", "bob", "hunter2", "1.4.0");
        Assert.Equal(RolloutStrategy.Staged, result.Value!.Strategy);
    }

    [Fact]
    public void History_AccumulatesAcrossRollouts()
    {
        var service = CreateService();
        service.StartRollout("cust-002", "bob", "hunter2", "1.4.0");
        service.StartRollout("cust-002", "bob", "hunter2", "1.4.1");
        Assert.Equal(2, service.History.Count);
    }

    [Fact]
    public void StagedRolloutPlanner_SplitsIntoThreeWaves()
    {
        var planner = new StagedRolloutPlanner();
        var waves = planner.PlanWaveSizes(1000);
        Assert.Equal(3, waves.Count);
        Assert.Equal(1000, waves.Sum());
    }

    [Fact]
    public void StagedRolloutPlanner_FirstWaveIsSmallest()
    {
        var planner = new StagedRolloutPlanner();
        var waves = planner.PlanWaveSizes(1000);
        Assert.True(waves[0] <= waves[1]);
        Assert.True(waves[1] <= waves[2]);
    }

    [Fact]
    public void StagedRolloutPlanner_NegativeTotal_Throws()
    {
        var planner = new StagedRolloutPlanner();
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.PlanWaveSizes(-1));
    }
}
