using Xunit;

namespace Trunk.ImpactedTargets.Tests;

public class ProjectGraphAnalyzerTests
{
    private static ProjectGraphAnalyzer CreateAnalyzer() => new(RepoRootLocator.FindSolutionFile());

    [Fact]
    public void FindOwningProject_FileInsideProjectDirectory_ReturnsProjectName()
    {
        var analyzer = CreateAnalyzer();
        var owner = analyzer.FindOwningProject(RepoRootLocator.Find(), "src/Telemetry.Client/TelemetryBatcher.cs");
        Assert.Equal("Telemetry.Client", owner);
    }

    [Fact]
    public void FindOwningProject_FileOutsideAnyProject_ReturnsNull()
    {
        var analyzer = CreateAnalyzer();
        var owner = analyzer.FindOwningProject(RepoRootLocator.Find(), "README.md");
        Assert.Null(owner);
    }

    [Fact]
    public void ExpandToDependents_LeafProject_ReturnsOnlyItselfAndItsTests()
    {
        // Telemetry.Client depends only on Common.Core and nothing else depends on
        // Telemetry.Client, so a change there should stay in a tiny lane: itself
        // plus its own test project.
        var analyzer = CreateAnalyzer();
        var dependents = analyzer.ExpandToDependents(new[] { "Telemetry.Client" });

        Assert.Equal(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Telemetry.Client", "Telemetry.Client.Tests" },
            dependents);
    }

    [Fact]
    public void ExpandToDependents_FeatureFlags_ReturnsFullDependentClosure()
    {
        // The money-shot scenario: Common.FeatureFlags is depended on (directly or
        // transitively) by nearly every service, so a change there should collapse
        // to almost the entire graph.
        var analyzer = CreateAnalyzer();
        var dependents = analyzer.ExpandToDependents(new[] { "Common.FeatureFlags" });

        Assert.Contains("Common.FeatureFlags", dependents);
        Assert.Contains("Identity.Service", dependents);
        Assert.Contains("DeviceManagement.Service", dependents);
        Assert.Contains("AppDelivery.Service", dependents); // transitive, via Identity.Service
        Assert.Contains("Identity.Service.Tests", dependents);
        Assert.Contains("DeviceManagement.Service.Tests", dependents);
        Assert.Contains("AppDelivery.Service.Tests", dependents);
        Assert.Contains("Common.FeatureFlags.Tests", dependents);

        // Telemetry.Client never references FeatureFlags, so it must NOT be pulled in.
        Assert.DoesNotContain("Telemetry.Client", dependents);
        Assert.DoesNotContain("Telemetry.Client.Tests", dependents);
    }

    [Fact]
    public void ExpandToDependents_IdentityService_IncludesCrossServiceDependent()
    {
        // AppDelivery.Service is the one deliberate cross-service edge in the graph.
        var analyzer = CreateAnalyzer();
        var dependents = analyzer.ExpandToDependents(new[] { "Identity.Service" });

        Assert.Contains("Identity.Service", dependents);
        Assert.Contains("AppDelivery.Service", dependents);
        Assert.DoesNotContain("DeviceManagement.Service", dependents);
    }

    [Fact]
    public void ExpandToDependents_UnknownProjectName_IsIgnoredNotThrown()
    {
        var analyzer = CreateAnalyzer();
        var dependents = analyzer.ExpandToDependents(new[] { "Does.Not.Exist" });
        Assert.Empty(dependents);
    }

    [Fact]
    public void AllProjectNames_IncludesEveryKnownProject()
    {
        var analyzer = CreateAnalyzer();
        Assert.Contains("Common.Core", analyzer.AllProjectNames);
        Assert.Contains("AppDelivery.Service", analyzer.AllProjectNames);
        Assert.True(analyzer.AllProjectNames.Count >= 12);
    }
}
