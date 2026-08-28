**status: active**

# CI Caching & Selective Execution — Design

## Problem

CI compiles the .NET solution up to three times per PR (ci.yml `build`, CodeQL csharp,
the OpenAPI drift composite action) and runs every job on every PR regardless of what
changed. With the self-hosted turbo remote cache now live for the JS side, the question
was whether wrapping `dotnet build` in a turbo task would let the remote cache eliminate
C# rebuilds too.

## Research findings (2026-08-28 session)

Three parallel investigations (repo/CI audit, Turborepo docs + prior art, MSBuild
incremental-build semantics) established:

1. **Turbo cannot deliver .NET incrementality.** Turbo's cache is all-or-nothing per
   task hash — there is no `restore-keys`-style "closest previous build" restore. Any
   change under `api/` would be a total miss restoring nothing: a fully cold rebuild,
   colder than today's `actions/cache` path. The only turbo win (skip when `api/` is
   untouched) is achievable with a plain path filter, no cache traffic at all.
2. **`obj/` is not portable.** It embeds absolute repo and NuGet paths
   (`project.assets.json` `packageFolders`, `nuget.g.props`, ref-assembly reference
   lists). The turbo remote cache is shared with laptops over the tailnet, so CI↔local
   artifact exchange would break exactly the way the Nx .NET plugin did
   (nrwl/nx#33684, `CS0006`). Turbo also tars with `mtime=0`, restores with
   mtime=now, merges over existing output dirs without cleaning, and the ducktors
   server defaults to a 100 MB per-artifact `BODY_LIMIT` with upload failures surfacing
   only as warnings. The Turborepo team's own Cargo integration refuses to cache
   compiler intermediate state for the same reasons.
3. **The existing `build-v3` cache likely skips no compilation.** MSBuild's up-to-date
   check is pure mtime comparison; git stamps every checked-out source with
   checkout time (newer than any tar-restored output), so warm builds still recompile
   everything. What the cache genuinely saves is NuGet restore/asset work. This is
   measurable locally (count `CoreCompile` executions on a simulated warm build).

## Decision

**Do not wrap `dotnet build` in a turbo task.** Recorded here so the idea is not
re-litigated: the mechanism (package.json in `api/`, workspace membership, turbo
`outputs` over `bin`/`obj`) is documented and possible, but delivers no incremental
rebuild, risks cross-machine `obj/` corruption through the shared cache, and silently
degrades on the 100 MB artifact limit.

Instead, take the wins the research actually surfaced:

| # | Change | Mechanism | Expected saving |
|---|--------|-----------|-----------------|
| 1 | Measure `build-v3` compile incrementality | Local simulation, findings recorded | Informs #2 (no CI change) |
| 2 | Make `build-v3` actually incremental | `git-restore-mtime` before build in ci.yml/deploy.yml (only if #1 confirms the placebo) | Warm `build` job drops from full compile to delta compile |
| 3 | Path-gate ci.yml jobs | `changes` job (dorny/paths-filter) + job `if`s | JS-only PRs skip 3 .NET test jobs; api-only PRs skip fork-smoke; docs-only PRs skip everything |
| 4 | Path-filter CodeQL + drop analyzers there | `on.pull_request.paths` + `-p:RunAnalyzers=false` | No CodeQL on JS/docs PRs; csharp leg loses the analyzer tax (analyzers are often the largest slice of .NET build time; ci.yml `build` still enforces them) |
| 5 | Drop analyzers in the OpenAPI emission build | `-p:RunAnalyzers=false` in the composite action | Faster openapi-drift / autoregen |
| 6 | Route route-tree-drift through turbo | `turbo run build --filter` per app + TURBO_* + Tailscale | Package/app builds become remote-cache hits |
| 7 | Route sdk-publish through turbo | Root `turbo run build test --filter @bc-solutions-coder/sdk` + TURBO_* + Tailscale | Rare runs, cheap change |
| 8 | actionlint gate for workflows | `scripts/lint-actions.sh` + `pnpm lint:actions` + a path-filtered `actionlint.yml` workflow | Catches workflow-syntax/expression bugs before they burn a CI run; also the verification tool every other change in this plan runs before landing |

## Design details & risk notes

- **#2 correctness risk (accepted, pre-release):** with commit-time mtimes, MSBuild's
  known false-skip classes (deleted/renamed inputs, equal timestamps) can leave a stale
  assembly in a warm build. Mitigations: deterministic builds are already on; the
  escape hatch is bumping the cache namespace (`build-v3` → `build-v4`), which forces a
  cold build — the same lever already used for the Wallow-gwy2-era cache pollution
  incident. `git-restore-mtime` needs `fetch-depth: 0` on checkout.
- **#3 gating semantics:** `build` gates on `code` (everything except `docs/**` and
  `**/*.md`); the docker/e2e jobs inherit the skip through `needs: build`. The .NET
  image is needed by e2e even on JS-only PRs, so `build`/`docker-images-*`/`e2e-tests`
  stay ungated apart from the docs-only case. `merge-coverage`'s existing
  `if: always() && (…success…)` already degrades to skipped when both test jobs skip.
  GitHub treats `if:`-skipped jobs as passing for required status checks, so branch
  protection (if added later) keeps working.
- **#4:** CodeQL's `--no-incremental` stays — CodeQL must observe every compilation.
  Only the analyzers go. The weekly cron run still covers `main` unconditionally, so a
  path-filtered PR trigger loses no coverage class.
- **#5:** `RunAnalyzers=false` maps to csc `/skipanalyzers`, which skips *diagnostic*
  analyzers but still runs source generators (`[LoggerMessage]` et al.) — a successful
  build is itself the proof, since the solution cannot compile without generator
  output.
- **#6 cache-hit soundness:** each app's turbo `outputs` includes `routeTree.gen.ts`
  and its `inputs` exclude it, so the task hash covers exactly the route sources; a
  restored tree is byte-equivalent to a regenerated one, and the `git diff` drift check
  is unaffected.
- **#7:** `pnpm test`/`pnpm build` in sdk-publish currently run from
  `packages/sdk` (job-level `working-directory`), bypassing turbo entirely — they are
  always cold today.
- **#8:** actionlint lands **first** — every other change here edits workflow YAML and
  verifies with it. The local script prefers an `actionlint` on PATH and falls back to
  the pinned `rhysd/actionlint` docker image, so no new toolchain is mandatory.
  `lint:actions` stays **out of `pnpm check`**: `check` is deliberately runnable
  offline, and the docker fallback needs a one-time image pull. CI coverage comes from
  a dedicated `actionlint.yml` workflow path-filtered to `.github/**`. CLAUDE.md's
  root-script census must be updated in the same commit (it counts and names the
  scripts).

## Success criteria

- JS-only PR: `unit-tests`, `integration-tests`, `cross-tenant-tests`, CodeQL all
  skipped; docs-only PR: the whole CI workflow and CodeQL skipped.
- If #2 lands: warm ci.yml `build` job shows `CoreCompile` skipped for unchanged
  projects (verified in the simulation before landing, observed in Actions after).
- route-tree-drift on an unchanged-routes PR: turbo reports cache hits for the
  package builds.
- No workflow correctness regressions: a PR touching `api/` still runs every .NET
  job exactly as today.

Implementation plan: `docs/plans/2026-08-28/1527-ci-caching-plan.md`.

## Measurement (Task 2 result)

**Verdict: PLACEBO.** The `build-v3` cache short-circuits **no** compilation — a warm
build recompiles all 58 projects, exactly as research finding #3 predicted — and
`git-restore-mtime` fixes it completely (0 recompiled, 58 skipped). **Task 4 proceeds.**

Measured locally on 2026-08-28 (8-core linux, .NET SDK 10.0.302, warm NuGet cache), by
the plan's three-scenario simulation: fresh clone per scenario, `dotnet restore` then
`dotnet build api/Wallow.slnx --no-restore -c Release --graph -v:normal`, with the cold
scenario's `**/bin/Release` + `**/obj` tarred (mtimes preserved) as the stand-in for
`actions/cache`.

| scenario | CoreCompile executed | skipped | wall |
|----------|---------------------|---------|------|
| cold  | 58 | 0  | 23s |
| warm  | 58 | 0  | 20s |
| mtime | 0  | 58 | 7s  |

Counters: *executed* = `Compilation request ` lines (one per real csc invocation);
*skipped* = `Skipping target "CoreCompile"` lines. The plan's `^CoreCompile:` grep does
**not** work — at `-v:normal` the console logger indents target headers and prefixes
them with a node id (`    3>CoreCompile:`), and re-emits the header when a project's
output resumes on another node, so the header count (118–131 on a cold build of 58
projects) counts neither executions nor projects. A control run — rebuilding an
already-built tree in place, nothing changed — reported executed=0, skipped=58, 3.6s,
which is what validates both counters.

**One trap worth recording: the workspace path must be identical across scenarios.**
The plan's script clones each scenario into its own directory (`$WORK/cold`,
`$WORK/warm`, `$WORK/mtime`). Run that way, `mtime` *also* recompiled all 58 projects —
which would have (wrongly) closed Task 4 as a no-op. Cause is design finding #2:
`obj/` is not path-portable, so restoring cold's `obj/` under a different directory
makes `dotnet restore` rewrite `project.assets.json` / `*.nuget.g.props` with fresh
mtimes, and those are compile inputs — every project is then out of date no matter what
the source mtimes say. A GitHub runner always checks out to the same absolute path, so
the numbers above come from the corrected variant where every scenario builds at one
fixed path; there `restore` no-ops and the restored outputs stay newer than their
inputs. Anyone re-running this measurement must keep the path fixed.

Sizes for context: the tarred cache is ~2.1 GB. Wall times are from a fast local
machine with a warm NuGet cache and are useful only as ratios — the shape (`warm` ≈
`cold`, `mtime` ≈ ⅓) is the result, not the absolute seconds.
