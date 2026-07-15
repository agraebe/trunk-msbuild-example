using Microsoft.Build.Graph;

namespace Trunk.ImpactedTargets;

/// <summary>
/// Owns everything that touches Microsoft.Build.Graph.ProjectGraph: loading the
/// static graph for the solution, mapping a changed file to the project that owns
/// it, and expanding a set of directly-changed projects to their full transitive
/// dependent closure (reverse edges). Deliberately has no knowledge of git or HTTP —
/// that separation is what makes this class unit-testable against the real solution
/// in this repo without a git checkout or network access. See
/// Trunk.ImpactedTargets.Tests/ProjectGraphAnalyzerTests.cs.
///
/// IMPORTANT: call MsBuildRegistration.EnsureRegistered() before constructing this
/// type, anywhere in the process. This class does not do it itself so that unit
/// tests can register once for the whole test assembly (see AssemblyFixture).
/// </summary>
public sealed class ProjectGraphAnalyzer
{
    private readonly ProjectGraph _graph;
    private readonly Dictionary<string, ProjectGraphNode> _nodesByProjectName;
    private readonly List<(string NormalizedDirectory, string ProjectName)> _directoriesByProjectName;

    public ProjectGraphAnalyzer(string solutionOrProjectPath)
    {
        _graph = new ProjectGraph(solutionOrProjectPath);

        _nodesByProjectName = new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);
        _directoriesByProjectName = new List<(string, string)>();

        foreach (var node in _graph.ProjectNodes)
        {
            var name = GetProjectName(node);
            _nodesByProjectName[name] = node;

            var directory = Path.GetDirectoryName(node.ProjectInstance.FullPath)!;
            _directoriesByProjectName.Add((NormalizePath(directory), name));
        }

        // Longest directory first, so a nested project directory wins over its parent
        // when matching a changed file path (there are no nested projects in this
        // sample repo, but a real monorepo will have them).
        _directoriesByProjectName.Sort((a, b) => b.NormalizedDirectory.Length.CompareTo(a.NormalizedDirectory.Length));
    }

    /// <summary>All project names known to the graph, for callers that need "everything".</summary>
    public IReadOnlyCollection<string> AllProjectNames => _nodesByProjectName.Keys;

    /// <summary>
    /// Finds the project that owns a repo-relative file path by longest matching
    /// directory prefix. Returns null if no project directory contains the path
    /// (e.g. a top-level README or CI workflow file — those are handled by
    /// PathRules, not the graph).
    /// </summary>
    public string? FindOwningProject(string repoRootAbsolutePath, string repoRelativeFilePath)
    {
        var absoluteFilePath = NormalizePath(Path.Combine(repoRootAbsolutePath, repoRelativeFilePath));

        foreach (var (directory, projectName) in _directoriesByProjectName)
        {
            if (absoluteFilePath.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase) ||
                absoluteFilePath.Equals(directory, StringComparison.OrdinalIgnoreCase))
            {
                return projectName;
            }
        }

        return null;
    }

    /// <summary>
    /// Expands a set of directly-changed project names to the full transitive
    /// dependent closure: every project that references a changed project, directly
    /// or through another project, plus the changed projects themselves. This is a
    /// reverse breadth-first traversal over ProjectGraphNode.ReferencingProjects —
    /// the edge direction ProjectGraph exposes specifically for "who depends on me".
    /// </summary>
    public IReadOnlySet<string> ExpandToDependents(IEnumerable<string> changedProjectNames)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var name in changedProjectNames)
        {
            if (!_nodesByProjectName.ContainsKey(name))
            {
                continue;
            }

            if (result.Add(name))
            {
                queue.Enqueue(name);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var node = _nodesByProjectName[current];

            foreach (var referencingNode in node.ReferencingProjects)
            {
                var referencingName = GetProjectName(referencingNode);
                if (result.Add(referencingName))
                {
                    queue.Enqueue(referencingName);
                }
            }
        }

        return result;
    }

    private static string GetProjectName(ProjectGraphNode node) =>
        Path.GetFileNameWithoutExtension(node.ProjectInstance.FullPath);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
}
