# trunk-msbuild-example

A reference implementation for feeding Trunk Merge Queue's dynamic parallel
lanes from an MSBuild/.NET monorepo. Trunk has first-party impacted-target
integrations for Bazel and Nx; for everything else — including MSBuild — Trunk
exposes a plain HTTP API and expects you to bring your own graph analysis.
This repo is that implementation: a small but structurally realistic .NET
monorepo, a console tool that computes impacted targets from MSBuild's own
static project graph, and the GitHub Actions wiring to run it on every PR.

## What this demonstrates

1. A small but structurally interesting .NET monorepo (6 src projects, 6 test
   projects, one console tool) with a real MSBuild project-reference graph.
2. **The money shot**: a PR touching the shared feature-flag library
   (`Common.FeatureFlags`) impacts almost every service — one merge-queue
   lane, no parallelism. A PR touching the isolated `Telemetry.Client` fans
   out into a tiny, cheap lane. Same repo, same tool, wildly different blast
   radius, computed automatically from the graph MSBuild already knows.
3. A console tool (`tools/Trunk.ImpactedTargets`) that uses
   `Microsoft.Build.Graph.ProjectGraph` — the same static graph engine behind
   `msbuild -graph` — to turn a git diff into an impacted-target list and
   POST it to Trunk's Merge Queue API.
4. Real JUnit XML output from xUnit, including three tests that are
   *intentionally* flaky, one per common flake class, wired up for Trunk's
   flaky-test detection to find.
5. GitHub Actions workflows showing where both pieces run in CI, verified
   against a live Trunk org: PRs post impacted targets, enter the real Merge
   Queue, and produce the expected lane graph (parallel lanes for
   non-overlapping changes, a single stacked lane when a change touches
   everything).

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

If you don't have an `origin/main` locally, use any two commits/branches you
have, e.g. `--base HEAD~1`.

## Repository layout

```
ContosoWorkspace.sln                  <- one solution, everything below it
Directory.Build.props                 <- shared TFM/nullable/lang-version settings
global.json                           <- pins the .NET 8 SDK
trunk-impacted-targets.config.json    <- repo-specific config the tool reads (see below)

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
  Trunk.ImpactedTargets/          <- the console app; a standalone, portable unit (see below)
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
  as an explicit rule in `trunk-impacted-targets.config.json` since MSBuild
  has no idea a JSON fixture feeds a C# class.
- A change to **Directory.Build.props**, the `.sln`, `global.json`, or the
  impacted-targets tool's own source is treated as impacting **everything**,
  conservatively — same config file, `buildInfrastructurePaths`.

## The impacted-targets tool

`tools/Trunk.ImpactedTargets` is a plain console app, not a shell/Python
script, because the audience is a .NET shop and it doubles as sample code to
fork. It's a **standalone, portable unit**: no `ProjectReference` to anything
under `src/`, no hardcoded project names, no assumption about your monorepo's
shape beyond "one `.sln` at the repo root." Every repo-specific fact — which
paths feed which projects outside the MSBuild graph, which paths are build
infrastructure — lives in one JSON config file the tool reads at runtime, not
in code. That's the same shape as Trunk's own
[`bazel-action`](https://github.com/trunk-io/bazel-action/tree/main/src/scripts):
a generic engine, parameterized by config, with zero hardcoded knowledge of
any specific workspace baked into the engine itself.

It's split into small, single-purpose, mostly-unit-tested classes:

| File | Responsibility |
|---|---|
| `MsBuildRegistration.cs` | Binds to the installed SDK's real MSBuild assemblies via `Microsoft.Build.Locator`. **Must run before any other `Microsoft.Build.*` type is touched** — see the comment in that file for the exact failure mode if you get this wrong. |
| `ProjectGraphAnalyzer.cs` | Loads `Microsoft.Build.Graph.ProjectGraph` for the solution; maps a changed file to its owning project (longest directory-prefix match); expands a set of directly-changed projects to their full transitive **dependents** via `ProjectGraphNode.ReferencingProjects`. Pure engine — no git, no HTTP, no repo-specific knowledge. |
| `PathRulesConfig.cs` | Loads and models `trunk-impacted-targets.config.json` — the file that makes this tool portable. **This is what you edit when adapting this to a real monorepo, not a `.cs` file.** |
| `PathRules.cs` | Applies a loaded `PathRulesConfig` to file paths. Pure engine — doesn't know what any of the configured names mean. |
| `ImpactedTargetsCalculator.cs` | Combines the graph + path rules over a list of changed files into a final `ImpactedTargetsResult` (either an explicit set, or "ALL"). |
| `GitDiffProvider.cs` | Shells out to `git diff --name-only base...head` (three-dot, so it diffs against the merge-base, not literally `base`). |
| `TrunkApiConstants.cs` | The Trunk API contract — endpoint, headers, payload shape — isolated in one file so it's the only place to update if Trunk changes it. |
| `TrunkApiClient.cs` | POSTs the result. |
| `Program.cs` / `CliOptions.cs` / `TrunkEnvironment.cs` | CLI parsing and env var wiring; ties the above together. |

### Dropping this into a different repo

The whole point of the split above is that `tools/Trunk.ImpactedTargets/` can
be lifted out of this repo wholesale:

1. Copy the `tools/Trunk.ImpactedTargets/` directory into your solution —
   anywhere your repo puts internal tooling — and add the `.csproj` to your
   `.sln`.
2. Write a `trunk-impacted-targets.config.json` at your repo root. Start from
   this repo's copy and strip it down: an empty `pathBasedRules` array is
   valid (you may have no non-MSBuild dependencies to encode yet), but
   `buildInfrastructurePaths` should at minimum list your shared
   `Directory.Build.props`/`.targets` files and `.sln`.
3. Copy `.github/workflows/impacted-targets.yml` and adjust the checkout step
   for your default branch name. Keep `fetch-depth: 0` and the explicit
   `TRUNK_PR_SHA` pin — see **Common pitfalls** below before you skip either.
4. Run `dotnet run --project <path-to-the-tool> -- --base origin/main
   --dry-run` locally against a real diff before wiring up the live POST.

Nothing else needs to change. If adapting this to your repo means editing
`ProjectGraphAnalyzer.cs` or `ImpactedTargetsCalculator.cs`, that's a sign the
config schema is missing something you need — extend the schema, don't
special-case the engine. See `tools/Trunk.ImpactedTargets/README.md` for the
full config schema reference.

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
  --base <ref> [--head <ref>] [--repo-root <path>] [--config <path>] [--dry-run]
```

- `--base` (required) — e.g. `origin/main`.
- `--head` — defaults to `HEAD`.
- `--repo-root` — defaults to the current directory.
- `--config` — path to the path-rules config; defaults to
  `<repo-root>/trunk-impacted-targets.config.json`.
- `--dry-run` — prints the JSON impact set, skips the POST.

Environment variables (only required for a live POST; ignored under
`--dry-run`), most of which fall back to GitHub Actions' own env vars so the
workflow doesn't have to redundantly wire them up:

| Variable | Falls back to | Purpose |
|---|---|---|
| `TRUNK_API_TOKEN` | — | org API token, sent as the `x-api-token` header (non-forked PRs) |
| `TRUNK_FORKED_WORKFLOW_RUN_ID` | — | forked-PR alternative to the token, sent as `x-forked-workflow-run-id`; set one or the other, not both |
| `TRUNK_REPO_OWNER` / `TRUNK_REPO_NAME` | parsed from `GITHUB_REPOSITORY` | repo identity |
| `TRUNK_REPO_HOST` | `github.com` | repo identity |
| `TRUNK_PR_NUMBER` | — | PR number |
| `TRUNK_PR_SHA` | `GITHUB_SHA` | head commit SHA — see the pitfall below before relying on the fallback |
| `TRUNK_TARGET_BRANCH` | `GITHUB_BASE_REF`, then `--base` | merge target branch |

### The Trunk API contract this tool calls

```
POST https://api.trunk.io:443/v1/setImpactedTargets
Content-Type: application/json
x-api-token: <org API token>          (non-forked PRs)
x-forked-workflow-run-id: <run id>    (forked PRs, instead of a token)

{
  "repo": { "host": "github.com", "owner": "...", "name": "..." },
  "pr": { "number": "123", "sha": "..." },
  "targetBranch": "main",
  "impactedTargets": ["Identity.Service", "AppDelivery.Service"]   // or the literal string "ALL"
}
```

`pr.number` is a JSON **string**, not an integer — match Trunk's own
reference client (`trunk-io/bazel-action`, `upload_impacted_targets.sh`,
which builds this field with `jq --arg`) rather than what the field name
suggests.

The JUnit upload step uses `trunk-io/analytics-uploader@v2`, which wraps the
`trunk-analytics-cli upload --junit-paths <glob> --org-url-slug <slug> --token
<token>` CLI.

### Common pitfalls

**`pr.sha` must be the PR's real head commit, not `GITHUB_SHA`.** For
`pull_request`-triggered workflows, GitHub Actions sets `GITHUB_SHA` to the
*ephemeral merge commit* (`refs/pull/N/merge`), not the PR branch's actual
head commit. Trunk's merge-queue readiness check
(`getSubmittedPullRequest.readiness.hasImpactedTargets`) keys uploaded
impacted targets to the PR's real head SHA. Upload under the merge-commit SHA
instead and `setImpactedTargets` still returns `200 OK` — Trunk accepts and
stores the payload — but the queue's readiness check never finds it, because
it's filed under a SHA the PR record doesn't track. The PR sits in "Waiting
to Enter Queue" indefinitely with no error anywhere. Set `TRUNK_PR_SHA` (or
the `--head` argument) explicitly to `github.event.pull_request.head.sha`, as
`.github/workflows/impacted-targets.yml` does — don't rely on the
`GITHUB_SHA` fallback for this event type.

If a PR looks stuck in "Not Ready" with GitHub reporting the PR as mergeable
and all checks green, compare the exact SHA in your last `setImpactedTargets`
request against the `prSha` field in a `getSubmittedPullRequest` response for
that PR (`POST /v1/getSubmittedPullRequest`, same repo/pr/targetBranch
shape). A mismatch there is the tell.

## Tests, including the flaky ones

Every test project references `JunitXml.TestLogger` and is run with:

```bash
dotnet test <project> --logger "junit;LogFilePath=junit.xml"
```

That produces a real `junit.xml` next to the project — this is what
`.github/workflows/tests.yml` globs up with `**/junit.xml` and hands to
`trunk-io/analytics-uploader@v2`.

**Exactly three tests in this repo are intentionally flaky**, each in a file
named `FlakyTests.cs` with a loud banner comment, each demonstrating a
distinct real-world flake class:

| Project | Flake class | Mechanism |
|---|---|---|
| `DeviceManagement.Service.Tests` | Random failure | Unseeded `Guid.NewGuid()` roll; fails ~25% of runs by genuine chance, not a hidden fixed seed. |
| `AppDelivery.Service.Tests` | Timing / tight timeout | Races real async work against a timeout tight enough to lose under CI load — the same shape as a Playwright test waiting on a selector. |
| `Identity.Service.Tests` | Shared mutable state / race condition | Two test classes race a shared static counter, synchronized via a `Barrier` so the interleaving is real OS-scheduling nondeterminism, not build-order luck — each test flips between pass and fail across runs. |

**There are no retries anywhere in this repo** — not in the test projects,
not in the GitHub Actions workflows. Retrying a flaky test on failure hides
exactly the signal Trunk's flaky-test detection is built to find. If you're
adapting this to a real pipeline and you have retry logic today, that's the
first thing to rip out before turning on flaky-test detection.

## Adapting this to a real monorepo

1. **`trunk-impacted-targets.config.json` is the only thing you should need
   to edit.** See "Dropping this into a different repo" above for the copy
   steps and `tools/Trunk.ImpactedTargets/README.md` for the full schema.
   Every enterprise monorepo accumulates a handful of "everything reads this"
   artifacts that live outside the MSBuild graph — config templates, schema
   files, generated code, shared PowerShell/bash helpers. Enumerate them
   explicitly the same way `data/seed/` is handled here: file-path prefix →
   set of project names to treat as directly changed. Resist making this
   "smart" (glob-and-guess); a wrong guess here silently under-tests PRs.
2. **The build-infrastructure "impact everything" list** (same config file)
   needs your repo's real shared files: central package management props,
   custom `.targets`, shared CI templates, `Directory.Packages.props`, etc.
3. **The engine code should not need to change.** `ProjectGraphAnalyzer`,
   `PathRules`, and `ImpactedTargetsCalculator` operate purely on the MSBuild
   graph and a config object — neither knows anything project-specific. If
   your solution has multiple `.sln` files, change `Program.cs`'s
   `FindSolutionFile` to point at the right one (or pass individual entry
   projects to `ProjectGraph` instead of a solution) — that's a real code
   change, but it's the one legitimate exception to "just edit the config."
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
