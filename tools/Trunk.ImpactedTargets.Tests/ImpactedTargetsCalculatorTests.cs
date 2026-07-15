using Xunit;

namespace Trunk.ImpactedTargets.Tests;

public class ImpactedTargetsCalculatorTests
{
    private static ImpactedTargetsCalculator CreateCalculator()
    {
        var repoRoot = RepoRootLocator.Find();
        var analyzer = new ProjectGraphAnalyzer(RepoRootLocator.FindSolutionFile());
        return new ImpactedTargetsCalculator(analyzer, repoRoot);
    }

    [Fact]
    public void Compute_LeafProjectChange_ReturnsSmallSet()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(new[] { "src/Telemetry.Client/TelemetryBatcher.cs" });

        Assert.False(result.IsAll);
        Assert.Equal(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Telemetry.Client", "Telemetry.Client.Tests" },
            result.Targets);
    }

    [Fact]
    public void Compute_FeatureFlagsChange_ReturnsFullDependentClosure()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(new[] { "src/Common.FeatureFlags/JsonFileFeatureFlagProvider.cs" });

        Assert.False(result.IsAll);
        Assert.True(result.Targets.Count >= 8);
        Assert.Contains("AppDelivery.Service", result.Targets);
        Assert.DoesNotContain("Telemetry.Client", result.Targets);
    }

    [Fact]
    public void Compute_SeedDataChange_ImpactsSeedConsumersAndTheirDependents()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(new[] { "data/seed/devices.json" });

        Assert.False(result.IsAll);
        // The rule fires for Common.Core; every dependent of Common.Core (which is
        // everything) is then pulled in via the normal graph expansion.
        Assert.Contains("Common.Core", result.Targets);
        Assert.Contains("Telemetry.Client", result.Targets);
        Assert.Contains("Identity.Service", result.Targets);
    }

    [Fact]
    public void Compute_BuildInfrastructureChange_ReturnsAll()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(new[] { "Directory.Build.props" });

        Assert.True(result.IsAll);
    }

    [Fact]
    public void Compute_UnmappedFile_ContributesNoImpact()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(new[] { "README.md" });

        Assert.False(result.IsAll);
        Assert.Empty(result.Targets);
    }

    [Fact]
    public void Compute_NoChangedFiles_ReturnsEmptySet()
    {
        var calculator = CreateCalculator();
        var result = calculator.Compute(Array.Empty<string>());

        Assert.False(result.IsAll);
        Assert.Empty(result.Targets);
    }
}
