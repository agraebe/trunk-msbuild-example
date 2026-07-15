# trunk-msbuild-example

A worked example of feeding Trunk Merge Queue's dynamic parallel lanes from an
MSBuild/.NET monorepo. Bazel and Nx have first-party impacted-target
integrations with Trunk; MSBuild does not, so this repo is the reference
implementation for that gap. Written for a senior platform engineer deciding
whether/how to wire this into a real monorepo — no marketing, ~15 minutes to
read.

## What this demonstrates

1. A small but structurally interesting .NET monorepo (6 src projects, 6 test
   projects, one console tool) with a real MSBuild project-reference graph.
2. **The money shot**: a PR touching the shared feature-flag library
   (`Common.FeatureFlags`) has to be treated as impacting almost every
   service — one merge-queue lane, no parallelism. A PR touching the
   isolated `Telemetry.Client` fans out into a tiny, cheap lane. Same repo,
   same tool, wildly different blast radius, computed automatically from the
   graph MSBuild already knows.
3. A console tool (`tools/Trunk.ImpactedTargets`) that uses
   `Microsoft.Build.Graph.ProjectGraph` — the same static graph engine behind
   `msbuild -graph` — to turn a git diff into an impacted-target list and
   POST it to Trunk's Merge Queue API.
4. Real JUnit XML output from xUnit, including three tests that are
   *intentionally* flaky, one per common flake class, wired up for Trunk's
   flaky-test detection to find.
5. GitHub Actions workflows showing where both pieces run in CI.

## Prerequisites

- .NET 8 SDK (LTS) — `dotnet --version` should print `8.x`.
- `git`.
- Nothing else. No Docker, no database, no network access required except the
  optional final POST to Trunk's API.

## Quickstart (three commands)

```bash
# 1. Build the whole solution
dotnet build ContosoWorkspace.sln

# 2. Run every test project with JUnit XML output (the exact invocation CI uses)
dotnet test tests/Identity.Service.Tests --logger "junit;LogFilePath=junit.xml"
# ...repeat per test project, or see .github/workflows/tests.yml for the loop
# that runs all seven (six service/lib test projects + the tool's own tests).

# 3. Dry-run the impacted-targets tool against your local history
dotnet run --project tools/Trunk.ImpactedTargets -- --base origin/main --dry-run
```

If you don't have an `origin/main` locally (this repo isn't pushed anywhere
yet), use any two commits/branches you have, e.g. `--base HEAD~1`.

## Repository layout

```
ContosoWorkspace.sln          <- one solution, everything below it
Directory.Build.props         <- shared TFM/nullable/lang-version settings
global.json                   <- pins the .NET 8 SDK

data/seed/                    <- JSON fixtures read by Common.Core at runtime
  customers.json
  devices.json
  regions.json

src/
  Common.Core/                <- leaf: seed-data loader, Result<T>, IClock
  Common.FeatureFlags/        <- in-memory JSON-backed feature flags (the "hot" lib)
  Identity.Service/           <- auth + MFA gating
  DeviceManagement.Service/   <- device compliance/onboarding
  AppDelivery.Service/        <- app rollout; the one cross-service edge (-> Identity)
  Telemetry.Client/           <- isolated corner: depends on Core only

tests/
  <ProjectName>.Tests/        <- one xUnit project per src project
  .../FlakyTests.cs           <- exactly 3 across the repo, loudly commented

tools/
  Trunk.ImpactedTargets/          <- the console app (the actual deliverable)
  Trunk.ImpactedTargets.Tests/    <- unit tests for the graph/mapping logic

.github/workflows/
  tests.yml               <- build, test w/ JUnit, upload to Trunk (always, even on failure)
  impacted-targets.yml    <- runs the tool on every PR
```

## The dependency graph

```
                         ┌────────────────┐
                         │  Common.Core   │   (leaf; seed-data loader lives here)
                         └───────┬────────┘
                    ┌────────────┼─────────────────┐
                    │            │                 │
          ┌─────────▼──────┐     │        ┌────────▼─────────┐
          │ Common.Feature │     │        │ Telemetry.Client  │  <- isolated corner
          │     Flags      │     │        │ (Core only)       │     always its own lane
          └───┬───┬───┬────┘     │        └───────────────────┘
              │   │   │         (Core also referenced directly
              │   │   │          by every project below via Flags)
   ┌──────────▼┐ ┌▼─────────────────┐ ┌▼──────────────────────┐
   │ Identity  │ │ DeviceManagement │ │  (AppDelivery also     │
   │ .Service  │ │ .Service         │ │   depends on Identity, │
   └─────┬─────┘ └──────────────────┘ │   see arrow below)     │
         │                            └────────────┬───────────┘
         └───────────────────────────────────────▶  AppDelivery.Service
                     (the one deliberate cross-service edge)

  data/seed/*.json ──(custom path rule, NOT a ProjectReference)──▶ Common.Core
                                                                     and therefore
                                                                     everything above
```

Reading the graph for merge-queue purposes:

- A change to **Telemetry.Client** impacts only itself + its test project — a
  two-project lane.
- A change to **Common.FeatureFlags** impacts `Common.FeatureFlags`,
  `Identity.Service`, `DeviceManagement.Service`, `AppDelivery.Service` (via
  Identity), and all four of their test projects — effectively the whole repo
  minus Telemetry.Client. One lane, no parallelism, and that's *correct*: any
  of those services could behave differently after the flag library changes.
- A change to **Identity.Service** impacts itself, `AppDelivery.Service`
  (the cross-service edge), and both their tests — but *not*
  `DeviceManagement.Service`, which never references Identity.
- A change to **data/seed/*.json** impacts `Common.Core` and therefore (via
  the same reverse-closure expansion) everything that depends on it — encoded
  as an explicit rule since MSBuild has no idea a JSON fixture feeds a C#
  class. See `tools/Trunk.ImpactedTargets/PathRules.cs`.
- A change to **Directory.Build.props**, the `.sln`, `global.json`, or the
  impacted-targets tool's own source is treated as impacting **everything**,
  conservatively — see the same file for why.

## The impacted-targets tool

`tools/Trunk.ImpactedTargets` is a plain console app, not a shell/Python
script, because the audience is a .NET shop and it doubles as sample code to
fork. It's split into small, single-purpose, mostly-unit-tested classes:

| File | Responsibility |
|---|---|
| `MsBuildRegistration.cs` | Binds to the installed SDK's real MSBuild assemblies via `Microsoft.Build.Locator`. **Must run before any other `Microsoft.Build.*` type is touched** — see the comment in that file for the exact failure mode if you get this wrong. |
| `ProjectGraphAnalyzer.cs` | Loads `Microsoft.Build.Graph.ProjectGraph` for the solution; maps a changed file to its owning project (longest directory-prefix match); expands a set of directly-changed projects to their full transitive **dependents** via `ProjectGraphNode.ReferencingProjects`. No git, no HTTP — unit-tested in isolation. |
| `PathRules.cs` | The two hand-written rules for things MSBuild's graph can't see: `data/seed/*.json` → `Common.Core` (and therefore its dependents), and build-infrastructure files → everything. **This is the file you rewrite first when adapting this to a real monorepo.** |
| `ImpactedTargetsCalculator.cs` | Combines the graph + path rules over a list of changed files into a final `ImpactedTargetsResult` (either an explicit set, or "ALL"). |
| `GitDiffProvider.cs` | Shells out to `git diff --name-only base...head` (three-dot, so it diffs against the merge-base, not literally `base`). |
| `TrunkApiConstants.cs` | The Trunk API contract — endpoint, headers, payload shape — isolated in one file, verified against Trunk's published docs (see below) rather than guessed. |
| `TrunkApiClient.cs` | POSTs the result. |
| `Program.cs` / `CliOptions.cs` / `TrunkEnvironment.cs` | CLI parsing and env var wiring; ties the above together. |

### Why it fails loudly instead of returning an empty set

If the project graph can't be loaded, or `git diff` fails, the tool exits
non-zero and prints the exception instead of falling back to "no impacted
targets." An empty impact set looks identical to "this PR is a no-op" to the
merge queue — it would under-test a real change. A crashed CI step is
visible and gets triaged; a quietly-wrong empty array is not. See the
`try/catch` in `Program.cs`.

### CLI

```
dotnet run --project tools/Trunk.ImpactedTargets -- \
  --base <ref> [--head <ref>] [--repo-root <path>] [--dry-run]
```

- `--base` (required) — e.g. `origin/main`.
- `--head` — defaults to `HEAD`.
- `--repo-root` — defaults to the current directory.
- `--dry-run` — prints the JSON impact set, skips the POST.

Environment variables (only required for a live POST; ignored under
`--dry-run`), most of which fall back to GitHub Actions' own env vars so the
workflow doesn't have to redundantly wire them up:

| Variable | Falls back to | Purpose |
|---|---|---|
| `TRUNK_API_TOKEN` | — | org API token, sent as the `x-api-token` header |
| `TRUNK_REPO_OWNER` / `TRUNK_REPO_NAME` | parsed from `GITHUB_REPOSITORY` | repo identity |
| `TRUNK_REPO_HOST` | `github.com` | repo identity |
| `TRUNK_PR_NUMBER` | — | PR number |
| `TRUNK_PR_SHA` | `GITHUB_SHA` | head commit SHA |
| `TRUNK_TARGET_BRANCH` | `GITHUB_BASE_REF`, then `--base` | merge target branch |

### The Trunk API contract this tool calls

Fetched and verified from `docs.trunk.io` while building this example
(`merge-queue/optimizations/parallel-queues/api`), not invented from memory:

```
POST https://api.trunk.io:443/v1/setImpactedTargets
Content-Type: application/json
x-api-token: <org API token>          (non-forked PRs)
x-forked-workflow-run-id: <run id>    (forked PRs, instead of a token)

{
  "repo": { "host": "github.com", "owner": "...", "name": "..." },
  "pr": { "number": 123, "sha": "..." },
  "targetBranch": "main",
  "impactedTargets": ["Identity.Service", "AppDelivery.Service"]   // or the literal string "ALL"
}
```

The JUnit upload step uses `trunk-io/analytics-uploader@v2` (documented at
`flaky-tests/get-started/ci-providers/github-actions`), which wraps the
`trunk-analytics-cli upload --junit-paths <glob> --org-url-slug <slug> --token
<token>` CLI invocation documented at `flaky-tests/reference/cli-reference`.

## Tests, including the flaky ones

Every test project references `JunitXml.TestLogger` and is run with:

```bash
dotnet test <project> --logger "junit;LogFilePath=junit.xml"
```

That produces a real `junit.xml` next to the project — this is what
`.github/workflows/tests.yml` globs up with `**/junit.xml` and hands to
`trunk-io/analytics-uploader@v2`. It is not a placeholder; run the command
yourself and open the file.

**Exactly three tests in this repo are intentionally flaky**, each in a file
named `FlakyTests.cs` with a loud banner comment, each demonstrating a
distinct real-world flake class:

| Project | Flake class | Mechanism |
|---|---|---|
| `DeviceManagement.Service.Tests` | Random failure | Unseeded `Guid.NewGuid()` roll; fails ~25% of runs by genuine chance, not a hidden fixed seed. |
| `AppDelivery.Service.Tests` | Timing / tight timeout | Races real async work against a timeout tight enough to lose under CI load — the same shape as a Playwright test waiting on a selector. |
| `Identity.Service.Tests` | Shared mutable state / order dependency | Two tests read-and-increment the same static counter, each assuming it runs first. |

**There are no retries anywhere in this repo** — not in the test projects,
not in the GitHub Actions workflows. Retrying a flaky test on failure hides
exactly the signal Trunk's flaky-test detection is built to find. If you're
adapting this to a real pipeline and you have retry logic today, that's the
first thing to rip out before turning on flaky-test detection.

## Adapting this to a real monorepo

1. **`PathRules.cs` is the main thing to rewrite.** Every enterprise monorepo
   accumulates a handful of "everything reads this" artifacts that live
   outside the MSBuild graph — config templates, schema files, generated
   code, shared PowerShell/bash helpers. Enumerate them explicitly the same
   way `data/seed/` is handled here: file-path prefix → set of project names
   to treat as directly changed. Resist making this "smart" (glob-and-guess);
   a wrong guess here silently under-tests PRs.
2. **The build-infrastructure "impact everything" list** needs your repo's
   real shared files: central package management props, custom `.targets`,
   shared CI templates, Directory.Packages.props, etc.
3. **`ProjectGraphAnalyzer` and `ImpactedTargetsCalculator` should not need to
   change** — they operate purely on the MSBuild graph and don't know
   anything project-specific. If your solution has multiple `.sln` files,
   change `Program.cs`'s `FindSolutionFile` to point at the right one (or
   pass individual entry projects to `ProjectGraph` instead of a solution).
4. **Target naming**: this repo emits MSBuild project names directly
   (`Identity.Service`). Trunk's docs describe targets as "as expressive as a
   Bazel target or a folder name" — nothing stops you from emitting a coarser
   name (e.g. one target per top-level folder) if your project count is huge
   and per-project lanes would be too fine-grained.
5. **Auth for the POST**: swap `TRUNK_API_TOKEN`/`x-api-token` for
   `x-forked-workflow-run-id` in forked-PR workflows, per Trunk's docs.
6. **Flaky test classes**: the three flake shapes here (random, timing,
   shared state) are common but not exhaustive — resource contention,
   dependency-on-execution-order via test collections, and environment
   differences (locale, timezone, filesystem case-sensitivity) are other
   common ones worth deliberately seeding once you've adopted Trunk's flaky
   detection, so you have known-good signal to validate against.

## What was verified before calling this done

- `dotnet build ContosoWorkspace.sln` — clean build, all 13 projects.
- Full test suite run 5× — deterministic tests passed 5/5 every time; the
  flaky tests flipped at least once each across the 5 runs.
- `--dry-run` against three synthetic diffs (scratch commit, tool run,
  `git reset` back): a `Telemetry.Client` change produced exactly
  `["Telemetry.Client", "Telemetry.Client.Tests"]`; a `Common.FeatureFlags`
  change produced the full dependent closure; a `data/seed/*.json` change
  produced the seed-rule's project set. See the verification transcript in
  the PR/session that produced this repo for the exact JSON output of each.
