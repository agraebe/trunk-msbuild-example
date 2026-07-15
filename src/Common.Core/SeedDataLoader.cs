using System.Text.Json;
using Contoso.Common.Core.Models;

namespace Contoso.Common.Core;

/// <summary>
/// Reads the JSON fixtures under data/seed/. Every project that references
/// Common.Core copies these files to its own output directory (see the
/// &lt;None Include&gt; in Common.Core.csproj), so this loader always resolves
/// them relative to the running assembly rather than the source tree.
///
/// This is the "seed data" half of the impacted-targets demo: the tool treats
/// data/seed/*.json as a dependency of every project that (transitively)
/// references Common.Core, via a hand-written path rule rather than an MSBuild
/// project reference, because MSBuild has no notion of "this JSON file feeds
/// that C# class". See tools/Trunk.ImpactedTargets/PathRules.cs.
/// </summary>
public sealed class SeedDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _seedDirectory;

    public SeedDataLoader(string? seedDirectory = null)
    {
        _seedDirectory = seedDirectory ?? Path.Combine(AppContext.BaseDirectory, "seed");
    }

    public IReadOnlyList<Customer> LoadCustomers() => Load<Customer>("customers.json");

    public IReadOnlyList<Device> LoadDevices() => Load<Device>("devices.json");

    public IReadOnlyList<Region> LoadRegions() => Load<Region>("regions.json");

    private List<T> Load<T>(string fileName)
    {
        var path = Path.Combine(_seedDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Seed file '{fileName}' not found at '{path}'. " +
                "Confirm the consuming project copies data/seed/*.json to its output directory.",
                path);
        }

        using var stream = File.OpenRead(path);
        var items = JsonSerializer.Deserialize<List<T>>(stream, JsonOptions);
        return items ?? new List<T>();
    }
}
