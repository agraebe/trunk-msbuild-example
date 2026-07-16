# Trunk.ImpactedTargets

A standalone console app that computes Trunk Merge Queue impacted targets from
an MSBuild solution's static project graph and POSTs them to Trunk's API.
This directory is the reusable unit — copy it into any MSBuild/.NET repo with
minimal changes. It has no dependency on the rest of the `trunk-msbuild-example`
repo: no `ProjectReference` to any `src/` project, no assumption about
project names, no assumption about what your monorepo looks like beyond "one
`.sln` at the repo root" (adjustable — see below).

Everything repo-specific lives in one JSON config file this tool reads at
runtime, not in code. That's the same shape as Trunk's own
[`bazel-action`](https://github.com/trunk-io/bazel-action/tree/main/src/scripts):
a generic engine, parameterized entirely by inputs/config, with zero
hardcoded knowledge of any specific workspace.

## Copying this into your repo

1. Copy `tools/Trunk.ImpactedTargets/` (this directory) into your solution,
   anywhere convenient — `tools/`, `build/`, wherever your repo puts internal
   utilities. Add the `.csproj` to your `.sln`.
2. Write a `trunk-impacted-targets.config.json` at your repo root (schema
   below). Start from the one in this repo as a template.
3. Wire up a CI step per `.github/workflows/impacted-targets.yml` in the repo
   root — the important parts are `fetch-depth: 0` on checkout, and pinning
   `TRUNK_PR_SHA` to the PR's actual head SHA (see the **Common pitfalls**
   section in the root README before you skip this).
4. Run `dotnet run --project <path> -- --base origin/main --dry-run` locally
   to sanity-check before wiring the live POST.

Nothing else in this directory should need to change. If you find yourself
editing `ProjectGraphAnalyzer.cs` or `ImpactedTargetsCalculator.cs` to make
this work for your repo, that's a signal the config schema is missing
something — file the gap rather than hand-editing the engine, or the next
person who copies this loses the fix.

## Config schema (`trunk-impacted-targets.config.json`)

```json
{
  "pathBasedRules": [
    {
      "pathPrefix": "data/seed/",
      "impactedProjects": ["Common.Core"]
    }
  ],
  "buildInfrastructurePaths": [
    "Directory.Build.props",
    "Directory.Build.targets",
    "global.json",
    "NuGet.config",
    ".sln",
    ".github/workflows/",
    "tools/Trunk.ImpactedTargets/"
  ]
}
```

- **`pathBasedRules`** — repo artifacts that affect projects without being a
  `ProjectReference` edge MSBuild's graph can see: seed data, shared config
  files, generated schemas, anything read at runtime rather than referenced
  at build time. Each rule says "a changed file under this path prefix
  directly impacts these project names"; the tool then expands each named
  project to its normal transitive-dependent closure, same as any other
  change. Match by `path.StartsWith(pathPrefix)`, forward slashes,
  case-insensitive.
- **`buildInfrastructurePaths`** — paths where a change should conservatively
  impact *every* target, because reasoning about "which projects are really
  affected" isn't worth the risk of guessing wrong. Central package
  management props, custom `.targets` files, shared CI templates, the tool's
  own source. Entries ending in `/` match by prefix; entries without a
  trailing slash match by suffix (so `.sln` matches any solution file
  regardless of name).

Keep both lists short, explicit, and hand-maintained. Resist making matching
"smart" (globbing, content-sniffing) — a wrong guess here silently under-tests
a PR, which is worse than a human maintaining an explicit list. If the config
file is missing, the tool exits non-zero rather than silently proceeding with
no rules — a missing config could otherwise be mistaken for "this repo has no
non-MSBuild dependencies," which is rarely true.

## Files

| File | Responsibility |
|---|---|
| `MsBuildRegistration.cs` | Binds to the installed SDK's real MSBuild assemblies via `Microsoft.Build.Locator`. **Must run before any other `Microsoft.Build.*` type is touched** — see the comment in that file for the exact failure mode if you get this wrong. |
| `ProjectGraphAnalyzer.cs` | Loads `Microsoft.Build.Graph.ProjectGraph` for the solution; maps a changed file to its owning project (longest directory-prefix match); expands a set of directly-changed projects to their full transitive **dependents** via `ProjectGraphNode.ReferencingProjects`. Pure engine — no git, no HTTP, no repo-specific knowledge. |
| `PathRulesConfig.cs` | Loads and models `trunk-impacted-targets.config.json`. The only file that changes per-repo config shape, if you ever need to. |
| `PathRules.cs` | Applies a loaded `PathRulesConfig` to file paths. Pure engine — doesn't know what any of the configured names mean. |
| `ImpactedTargetsCalculator.cs` | Combines the graph + path rules over a list of changed files into a final `ImpactedTargetsResult` (either an explicit set, or "ALL"). |
| `GitDiffProvider.cs` | Shells out to `git diff --name-only base...head` (three-dot, so it diffs against the merge-base, not literally `base`). |
| `TrunkApiConstants.cs` | The Trunk API contract — endpoint, headers, payload shape — isolated in one file so it's the only place to update if Trunk changes it. |
| `TrunkApiClient.cs` | POSTs the result. |
| `Program.cs` / `CliOptions.cs` / `TrunkEnvironment.cs` | CLI parsing and env var wiring; ties the above together. |

See the root README for the CLI reference, environment variables, the full
Trunk API contract, and common pitfalls (in particular, the `TRUNK_PR_SHA`
one — it's the easiest way to silently break this).
