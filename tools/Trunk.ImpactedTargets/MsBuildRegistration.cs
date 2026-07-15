using Microsoft.Build.Locator;

namespace Trunk.ImpactedTargets;

/// <summary>
/// #1 GOTCHA when using Microsoft.Build.Graph.ProjectGraph outside of `dotnet build`
/// or MSBuild.exe: a plain console app has no idea which MSBuild assemblies to load
/// (there can be several SDKs installed, and the NuGet-referenced Microsoft.Build
/// package intentionally ships reference assemblies only — see ExcludeAssets="runtime"
/// in the .csproj). If you `new ProjectGraph(...)` before resolving this, you get a
/// FileNotFoundException or a MissingMethodException deep in Microsoft.Build.dll that
/// gives no hint the real problem is assembly binding.
///
/// Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults() finds the MSBuild that
/// ships with the installed .NET SDK and wires up assembly resolution so the real
/// Microsoft.Build.dll (the one MSBuild itself uses) loads instead. It must run
/// exactly once per process, and before any other Microsoft.Build.* type is touched
/// anywhere in the call stack — including indirectly, e.g. by JIT-ing a method that
/// merely has a Microsoft.Build parameter type. That's why Program.Main calls this
/// as its very first line, before importing anything else that touches the graph.
/// </summary>
public static class MsBuildRegistration
{
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        _registered = true;
    }
}
