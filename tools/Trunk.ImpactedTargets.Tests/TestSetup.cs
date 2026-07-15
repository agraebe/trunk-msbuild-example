using System.Runtime.CompilerServices;
using Trunk.ImpactedTargets;

namespace Trunk.ImpactedTargets.Tests;

/// <summary>
/// Registers MSBuildLocator once for the whole test assembly, before any test
/// touches ProjectGraphAnalyzer (and therefore Microsoft.Build.Graph.ProjectGraph).
/// A [ModuleInitializer] runs the first time the assembly is loaded, which is
/// early enough and — unlike a per-test xUnit fixture — guaranteed to run exactly
/// once regardless of test parallelization.
/// </summary>
internal static class TestSetup
{
    [ModuleInitializer]
    public static void Init() => MsBuildRegistration.EnsureRegistered();
}
