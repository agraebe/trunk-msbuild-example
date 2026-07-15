using Xunit;

namespace Trunk.ImpactedTargets.Tests;

public class PathRulesTests
{
    [Theory]
    [InlineData("data/seed/customers.json")]
    [InlineData("data/seed/devices.json")]
    public void IsSeedDataPath_MatchesFilesUnderSeedDirectory(string path)
    {
        Assert.True(PathRules.IsSeedDataPath(path));
    }

    [Fact]
    public void IsSeedDataPath_DoesNotMatchUnrelatedPath()
    {
        Assert.False(PathRules.IsSeedDataPath("src/Common.Core/SeedDataLoader.cs"));
    }

    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("global.json")]
    [InlineData("ContosoWorkspace.sln")]
    [InlineData(".github/workflows/tests.yml")]
    [InlineData("tools/Trunk.ImpactedTargets/Program.cs")]
    public void IsBuildInfrastructurePath_MatchesKnownInfraFiles(string path)
    {
        Assert.True(PathRules.IsBuildInfrastructurePath(path));
    }

    [Fact]
    public void IsBuildInfrastructurePath_DoesNotMatchOrdinaryServiceFile()
    {
        Assert.False(PathRules.IsBuildInfrastructurePath("src/Identity.Service/IdentityVerifier.cs"));
    }
}
