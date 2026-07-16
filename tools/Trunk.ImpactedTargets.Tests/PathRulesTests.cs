using Xunit;

namespace Trunk.ImpactedTargets.Tests;

/// <summary>
/// Tests PathRules against a synthetic in-memory config, not this repo's real
/// trunk-impacted-targets.config.json — PathRules is generic engine code, and
/// these tests should keep passing unmodified even if Contoso's config changes.
/// See ImpactedTargetsCalculatorTests for tests against the real repo config.
/// </summary>
public class PathRulesTests
{
    private static PathRules CreateRules() => new(new PathRulesConfig
    {
        PathBasedRules = new List<PathBasedRule>
        {
            new() { PathPrefix = "data/seed/", ImpactedProjects = new List<string> { "Common.Core" } },
        },
        BuildInfrastructurePaths = new List<string>
        {
            "Directory.Build.props",
            "global.json",
            ".sln",
            ".github/workflows/",
        },
    });

    [Theory]
    [InlineData("data/seed/customers.json")]
    [InlineData("data/seed/devices.json")]
    public void GetDirectlyImpactedProjects_MatchesFilesUnderConfiguredPrefix(string path)
    {
        var rules = CreateRules();
        Assert.Equal(new[] { "Common.Core" }, rules.GetDirectlyImpactedProjects(path));
    }

    [Fact]
    public void GetDirectlyImpactedProjects_DoesNotMatchUnrelatedPath()
    {
        var rules = CreateRules();
        Assert.Empty(rules.GetDirectlyImpactedProjects("src/Common.Core/SeedDataLoader.cs"));
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("global.json")]
    [InlineData("ContosoWorkspace.sln")]
    [InlineData(".github/workflows/tests.yml")]
    public void IsBuildInfrastructurePath_MatchesConfiguredMarkers(string path)
    {
        var rules = CreateRules();
        Assert.True(rules.IsBuildInfrastructurePath(path));
    }

    [Fact]
    public void IsBuildInfrastructurePath_DoesNotMatchOrdinaryServiceFile()
    {
        var rules = CreateRules();
        Assert.False(rules.IsBuildInfrastructurePath("src/Identity.Service/IdentityVerifier.cs"));
    }

    [Fact]
    public void GetDirectlyImpactedProjects_EmptyConfig_MatchesNothing()
    {
        var rules = new PathRules(new PathRulesConfig());
        Assert.Empty(rules.GetDirectlyImpactedProjects("data/seed/customers.json"));
        Assert.False(rules.IsBuildInfrastructurePath("Directory.Build.props"));
    }
}
// Stress 18: tool's own test project (isolated lane) (1784238741039551000)
