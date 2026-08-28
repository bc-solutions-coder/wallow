**status: active**

# CI Caching & Selective Execution — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. In this repo the tasks are executed as **beads** via the `spec-to-beads` + `team-build` skills — see the Bead Map at the end.

**Goal:** Cut CI wall time by making the .NET build cache genuinely incremental, skipping jobs whose inputs did not change, removing redundant analyzer passes, and putting the two turbo-bypassing JS workflows on the remote cache — plus an actionlint gate so workflow edits are verified before they burn a CI run.

**Architecture:** No turbo wrapper around `dotnet build` (rejected — see the design doc). Instead: `dorny/paths-filter` job-level gating in `ci.yml`, `git-restore-mtime` ahead of the existing `build-v3` cache, `-p:RunAnalyzers=false` on the two builds whose analyzers duplicate `ci.yml`'s, and `turbo run` + `TURBO_*` + Tailscale in `route-tree-drift.yml` / `sdk-publish.yml` mirroring `js.yml`.

**Tech Stack:** GitHub Actions, actionlint, dorny/paths-filter@v3, chetan/git-restore-mtime-action@v2, MSBuild/.NET 10 SDK, Turborepo 2.x + ducktors remote cache over Tailscale.

**Spec:** `docs/plans/2026-08-28/1526-ci-caching-design.md`

## Global Constraints

- Conventional commits: `<type>[scope]: <description>`, lowercase, imperative, first line < 72 chars. `ci`/`docs` types are non-releasing; use `perf(ci)` where the change is a performance win.
- Every workflow edit is verified with `pnpm lint:actions` (created in Task 1) before commit.
- `TURBO_API` / `TURBO_TEAM` / `TURBO_TOKEN` / `TS_OAUTH_CLIENT_ID` / `TS_OAUTH_SECRET` are existing repository secrets — reference them, never inline values. Fork PRs see empty secrets and must degrade to uncached runs, never fail.
- Do not touch `docker/*.yml` (keeps `pnpm lint:env` out of scope). If `package.json` changes, run `pnpm lint:manifests` and `pnpm lint:deps` too.
- CodeQL's csharp build keeps `--no-incremental` (CodeQL must observe every compilation).
- The three .NET *test* jobs and `docker-images-app` keep `fail-on-cache-miss: true` exactly as-is.
- Third-party action versions: look the current release up (ref.tools / GitHub releases) at implementation time; versions written below are known-good floors, not ceilings.

---

### Task 1: actionlint gate

**Files:**
- Create: `scripts/lint-actions.sh` (mode 755)
- Create: `.github/workflows/actionlint.yml`
- Modify: `package.json` (scripts block)
- Modify: `CLAUDE.md` (root-script census)

**Interfaces:**
- Produces: `pnpm lint:actions` — the verification command every later task runs before committing workflow edits.

- [ ] **Step 1: Pin the version.** Look up the latest actionlint release (`https://github.com/rhysd/actionlint/releases`). The snippets below say `1.7.7`; substitute the current version in **both** files, identically.

- [ ] **Step 2: Create `scripts/lint-actions.sh`:**

```bash
#!/usr/bin/env bash
# actionlint over .github/workflows. Prefers a local binary; falls back to the
# pinned docker image so no contributor has to install Go tooling. Deliberately
# NOT part of `pnpm check`: check stays runnable offline, and the docker
# fallback needs a one-time image pull.
set -euo pipefail
cd "$(dirname "$0")/.."

ACTIONLINT_VERSION="1.7.7"

if command -v actionlint >/dev/null 2>&1; then
  exec actionlint -color
fi

if command -v docker >/dev/null 2>&1; then
  exec docker run --rm -v "$PWD":/repo -w /repo \
    "rhysd/actionlint:${ACTIONLINT_VERSION}" -color
fi

echo "error: actionlint is not installed and docker is unavailable." >&2
echo "install guide: https://github.com/rhysd/actionlint/blob/main/docs/install.md" >&2
exit 1
```

Then `chmod +x scripts/lint-actions.sh`.

- [ ] **Step 3: Add the root script.** In `package.json`, after `"lint:env": "./scripts/check-env.sh",` insert:

```json
    "lint:actions": "./scripts/lint-actions.sh",
```

Do **not** add it to the `check` chain.

- [ ] **Step 4: Create `.github/workflows/actionlint.yml`:**

```yaml
name: Actionlint

# Workflow-file lint. Local twin: `pnpm lint:actions` (scripts/lint-actions.sh).
# Path-filtered: only workflow/action edits can break workflows.

on:
  pull_request:
    branches: [main]
    paths:
      - ".github/**"
  push:
    branches: [main]
    paths:
      - ".github/**"

concurrency:
  group: actionlint-${{ github.head_ref || github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  actionlint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      # Pinned download per actionlint's own install docs; the script emits an
      # `executable` step output pointing at the downloaded binary.
      - name: Download actionlint
        id: get_actionlint
        run: bash <(curl -fsSL https://raw.githubusercontent.com/rhysd/actionlint/v1.7.7/scripts/download-actionlint.bash) 1.7.7

      - name: Run actionlint
        run: ${{ steps.get_actionlint.outputs.executable }} -color
```

- [ ] **Step 5: Run it and triage findings.** `pnpm lint:actions` over the *existing* workflows. Fix genuine findings (undefined outputs, bad expressions, shell bugs) in this same commit. If the shellcheck integration produces pure noise on existing inline scripts, disable only shellcheck with `-shellcheck=` (empty value) in **both** the script and the workflow — do not suppress actionlint's own rules. Re-run until green.

- [ ] **Step 6: Update CLAUDE.md's script census.** In the root `CLAUDE.md` commands block, after the `pnpm lint:env` line add:

```
pnpm lint:actions            # scripts/lint-actions.sh — actionlint over .github/workflows (local binary or pinned docker fallback); NOT part of pnpm check — CI runs it via actionlint.yml
```

and change the trailing comment `# \`prepare\` (= \`husky\`) is the twentieth script` to `# \`prepare\` (= \`husky\`) is the twenty-first script`.

- [ ] **Step 7: Manifest gates.** `pnpm lint:manifests && pnpm lint:deps` (package.json changed). Expected: green; if knip flags `scripts/lint-actions.sh`, it is referenced from a root script, so investigate rather than ignore.

- [ ] **Step 8: Commit.**

```bash
git add scripts/lint-actions.sh .github/workflows/actionlint.yml package.json CLAUDE.md
git commit -m "ci: add actionlint gate for workflow files"
```

---

### Task 2: Measure whether the build-v3 cache short-circuits compilation

**Files:**
- Modify: `docs/plans/2026-08-28/1526-ci-caching-design.md` (append a "Measurement" section)
- No production files. The script below is a throwaway — run it from a temp dir, do not commit it.

**Interfaces:**
- Produces: a **verdict** recorded on the bead and in the design doc — `PLACEBO` (warm build recompiles everything; Task 4 proceeds) or `INCREMENTAL` (warm build skips unchanged projects; Task 4 closes as no-op). Plus the three CoreCompile executed/skipped counts and wall times.

- [ ] **Step 1: Write the script** to a temp dir (e.g. `$(mktemp -d)/measure.sh`):

```bash
#!/usr/bin/env bash
# Measures whether ci.yml's build-v3 cache actually short-circuits compilation.
# Three scenarios, each a fresh clone of the local repo:
#   cold  - no cache restored
#   warm  - cache restored, checkout-time source mtimes (CI today)
#   mtime - cache restored + git-restore-mtime (what Task 4 makes CI do)
set -euo pipefail
export MSBUILDTERMINALLOGGER=off

REPO=$(git rev-parse --show-toplevel)
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

build() { # $1 = clone dir, $2 = scenario name
  local start=$SECONDS
  (cd "$1" \
    && dotnet restore api/Wallow.slnx >/dev/null \
    && dotnet build api/Wallow.slnx --no-restore -c Release --graph -v:normal \
       > "$WORK/$2.log")
  local executed skipped
  executed=$(grep -c '^CoreCompile:' "$WORK/$2.log" || true)
  skipped=$(grep -c 'Skipping target "CoreCompile"' "$WORK/$2.log" || true)
  echo "$2: CoreCompile executed=$executed skipped=$skipped wall=$((SECONDS - start))s"
}

git clone --quiet "$REPO" "$WORK/cold"
build "$WORK/cold" cold

# Archive outputs the way actions/cache does: tar, original mtimes preserved.
(cd "$WORK/cold" && find . \( -path '*/bin/Release' -o -name obj \) -prune -print0 \
  | tar --null -T - -cf "$WORK/cache.tar")

git clone --quiet "$REPO" "$WORK/warm"
tar -C "$WORK/warm" -xf "$WORK/cache.tar"
build "$WORK/warm" warm

git clone --quiet "$REPO" "$WORK/mtime"
curl -fsSL -o "$WORK/git-restore-mtime" \
  https://raw.githubusercontent.com/MestreLion/git-tools/main/git-restore-mtime
(cd "$WORK/mtime" && python3 "$WORK/git-restore-mtime")
tar -C "$WORK/mtime" -xf "$WORK/cache.tar"
build "$WORK/mtime" mtime
```

- [ ] **Step 2: Sanity-check the grep patterns against the cold log.** The `cold` scenario must report `executed` ≈ the solution's project count (58 csproj; a handful may be non-compiling) and `skipped=0`. If both counters read 0, the logger format differs from the grep — inspect `$WORK/cold.log`, adjust the two grep patterns to the actual target-execution / target-skip lines, and re-run. Do not trust any verdict until the cold baseline is sane.

- [ ] **Step 3: Run it** (takes several minutes — three full Release builds of 58 projects). Record all three result lines.

- [ ] **Step 4: Verdict.** `warm` with `skipped` near 0 ⇒ **PLACEBO** (expected per research). `mtime` with `skipped` near the project count proves Task 4's fix works before touching CI; if `mtime` *also* recompiles everything, Task 4 must be closed as no-op with these numbers as the reason.

- [ ] **Step 5: Record.** Append to `docs/plans/2026-08-28/1526-ci-caching-design.md`:

```markdown
## Measurement (Task 2 result)

| scenario | CoreCompile executed | skipped | wall |
|----------|---------------------|---------|------|
| cold     | <n> | <n> | <n>s |
| warm     | <n> | <n> | <n>s |
| mtime    | <n> | <n> | <n>s |

Verdict: <PLACEBO|INCREMENTAL> — <one sentence>. Task 4 <proceeds|closes as no-op>.
```

(with the real numbers — the table cells above are the only permitted "placeholder" in this plan because they are this task's output). Put the same verdict in a `bd note` on the task's bead.

- [ ] **Step 6: Commit.**

```bash
git add docs/plans/2026-08-28/1526-ci-caching-design.md
git commit -m "docs(plans): record build-v3 incrementality measurement"
```

---

### Task 3: Path-gate ci.yml jobs

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `pnpm lint:actions` (Task 1).
- Produces: a `changes` job with outputs `code` / `dotnet` / `js` that Task 4's edits must not disturb.

- [ ] **Step 1: Confirm dorny/paths-filter semantics.** Check the README for the current major version (v3) and that negation patterns (`'!docs/**'`) are supported in filter lists (they are, since v2.7.0). Pin `@v3`.

- [ ] **Step 2: Insert the `changes` job** in `.github/workflows/ci.yml`, immediately after the `jobs:` line (before `build`):

```yaml
  # Classifies the PR's paths once; jobs gate on the outputs. `code` is
  # everything except docs — a docs-only PR skips the whole pipeline via
  # build's gate (docker/e2e inherit through `needs: build`). `dotnet` and
  # `js` gate only the test-side jobs: build/docker/e2e must run for BOTH
  # sides because e2e boots the full stack from this PR's images.
  changes:
    runs-on: ubuntu-latest
    permissions:
      pull-requests: read
    outputs:
      code: ${{ steps.filter.outputs.code }}
      dotnet: ${{ steps.filter.outputs.dotnet }}
      js: ${{ steps.filter.outputs.js }}
    steps:
      - name: Classify changed paths
        id: filter
        uses: dorny/paths-filter@v3
        with:
          filters: |
            code:
              - '**'
              - '!docs/**'
              - '!**/*.md'
            dotnet:
              - 'api/**'
              - 'global.json'
              - 'scripts/e2e.sh'
              - '.github/workflows/ci.yml'
            js:
              - 'apps/**'
              - 'packages/**'
              - 'package.json'
              - 'pnpm-lock.yaml'
              - 'pnpm-workspace.yaml'
              - '.nvmrc'
              - '.npmrc'
              - 'scripts/fork-smoke.sh'
              - '.github/workflows/ci.yml'
```

- [ ] **Step 3: Gate `build`.** Change the `build` job header from:

```yaml
  build:
    runs-on: ubuntu-latest
    permissions:
      contents: read
```

to:

```yaml
  build:
    needs: changes
    if: needs.changes.outputs.code == 'true'
    runs-on: ubuntu-latest
    permissions:
      contents: read
```

- [ ] **Step 4: Gate the three .NET test jobs.** For `unit-tests`, `integration-tests`, and `cross-tenant-tests`, change `needs: build` to:

```yaml
    needs: [changes, build]
    if: needs.changes.outputs.dotnet == 'true'
```

- [ ] **Step 5: Gate `fork-smoke`.** It currently has no `needs:` (there is a comment explaining that). Give it:

```yaml
    needs: changes
    if: needs.changes.outputs.js == 'true'
```

and update its `# No \`needs:\` — it touches no .NET output and can run alongside the build.` comment line to:

```yaml
  # Needs only `changes` (not build) — it touches no .NET output and can run
  # alongside the build; it gates on the js filter because a pure-api PR
  # cannot change what `pnpm pack` ships.
```

- [ ] **Step 6: Leave alone:** `docker-images-app`, `docker-images-infra`, `e2e-tests` (inherit the docs-only skip through `needs`), and `merge-coverage` (its `if: always() && (…success…)` already resolves to skipped when both test jobs skip — do not add `changes` to its `needs`, that would defeat the `always()`).

- [ ] **Step 7: Verify.** `pnpm lint:actions` → green. Then hand-check the four scenarios against the final YAML: docs-only (everything skips), api-only (fork-smoke skips, rest runs), js-only (three .NET test jobs + merge-coverage skip; build/docker/e2e run), mixed (everything runs).

- [ ] **Step 8: Commit.**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: skip .net test jobs and fork-smoke when their inputs are untouched"
```

---

### Task 4: Make build-v3 incremental with git-restore-mtime (GATED by Task 2)

**Files:**
- Modify: `.github/workflows/ci.yml` (build job only)
- Modify: `.github/workflows/deploy.yml` (build job only)

**Interfaces:**
- Consumes: Task 2's verdict (read the bead note / design doc). Task 3's ci.yml shape (this task edits the same file — apply on top).

- [ ] **Step 1: Read Task 2's verdict.** If `INCREMENTAL` (warm already skips) **or** the `mtime` scenario failed to skip: close this task as no-op, citing the numbers, and stop here.

- [ ] **Step 2: Edit the `build` job in `.github/workflows/ci.yml`.** Replace the bare checkout:

```yaml
      - uses: actions/checkout@v7
```

with:

```yaml
      # Full history: git-restore-mtime needs each file's last-touching commit.
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      # MSBuild's up-to-date check is mtime-based, and a fresh checkout stamps
      # every source newer than any cache-restored output — so without this the
      # restored bin/obj never short-circuits compilation (measured: see the
      # design doc's Measurement section). Restoring commit-time mtimes makes
      # only the files this PR touched look newer than the previous build.
      # Known false-skip classes (deleted/renamed inputs, equal timestamps) are
      # accepted pre-release; the escape hatch is bumping the cache namespace
      # (build-v3 -> build-v4) to force a cold build.
      - name: Restore source file mtimes
        uses: chetan/git-restore-mtime-action@v2
```

Only in the `build` job — the `--no-build` consumers (test jobs, docker-images-app) never compile, and their shallow checkouts stay fast.

- [ ] **Step 3: Apply the identical change** to the `build` job in `.github/workflows/deploy.yml` (same two edits: `fetch-depth: 0` + the mtime step between checkout and the NuGet cache step).

- [ ] **Step 4: Verify.** `pnpm lint:actions` → green.

- [ ] **Step 5: Commit.**

```bash
git add .github/workflows/ci.yml .github/workflows/deploy.yml
git commit -m "perf(ci): restore source mtimes so the build cache is actually incremental"
```

- [ ] **Step 6: Post-merge observation note.** Add a `bd note` on the bead: after the next two merged PRs, open the ci.yml `build` job log and confirm `CoreCompile` skips for untouched projects; if the job instead slows down or misbehaves, revert this commit and bump the cache namespace.

---

### Task 5: CodeQL — path filter + drop analyzers

**Files:**
- Modify: `.github/workflows/codeql.yml`

- [ ] **Step 1: Path-filter the PR trigger.** Change:

```yaml
on:
  pull_request:
    branches: [main]
  schedule:
    - cron: "0 6 * * 1"
```

to:

```yaml
on:
  pull_request:
    branches: [main]
    # csharp analyzes api/, actions analyzes .github/ — a PR touching neither
    # has nothing for CodeQL to scan. The weekly cron below still sweeps main
    # unconditionally, so nothing escapes coverage.
    paths:
      - "api/**"
      - "global.json"
      - ".github/**"
  schedule:
    - cron: "0 6 * * 1"
```

- [ ] **Step 2: Drop analyzers from the manual build.** Change:

```yaml
        run: dotnet build api/Wallow.slnx --no-restore --no-incremental --graph
```

to:

```yaml
        # --no-incremental stays: CodeQL must observe every compilation.
        # RunAnalyzers=false (csc /skipanalyzers) skips the four diagnostic
        # analyzer packages — ci.yml's build job enforces those — but still
        # runs source generators, which the build cannot succeed without.
        run: dotnet build api/Wallow.slnx --no-restore --no-incremental --graph -p:RunAnalyzers=false
```

- [ ] **Step 3: Local proof that generators survive `RunAnalyzers=false`:**

```bash
dotnet build api/Wallow.slnx -c Release --graph -p:RunAnalyzers=false
```

Expected: green (a `[LoggerMessage]`-using solution cannot compile if generators were skipped). This also warms nothing permanently — it is a plain local build.

- [ ] **Step 4: Verify + commit.**

```bash
pnpm lint:actions
git add .github/workflows/codeql.yml
git commit -m "perf(ci): path-filter codeql and drop analyzers from its build"
```

---

### Task 6: OpenAPI emission build — drop analyzers

**Files:**
- Modify: `.github/actions/openapi-document/action.yml`

- [ ] **Step 1: Edit the emission build.** In the `Emit OpenAPI document at build time` step, change:

```bash
        dotnet build api/src/Wallow.Api/Wallow.Api.csproj \
          --configuration Release \
          -p:OpenApiGenerateDocumentsOnBuild=true
```

to:

```bash
        # RunAnalyzers=false: this build exists to emit the document, not to
        # enforce analysis — ci.yml's build job owns that. Source generators
        # still run (csc /skipanalyzers skips only diagnostic analyzers).
        dotnet build api/src/Wallow.Api/Wallow.Api.csproj \
          --configuration Release \
          -p:OpenApiGenerateDocumentsOnBuild=true \
          -p:RunAnalyzers=false
```

- [ ] **Step 2: Verify.** `pnpm lint:actions` (actionlint checks workflows, not composite actions — also YAML-parse the file: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/actions/openapi-document/action.yml'))"`). Both green.

- [ ] **Step 3: Commit.**

```bash
git add .github/actions/openapi-document/action.yml
git commit -m "perf(ci): drop analyzers from the openapi emission build"
```

---

### Task 7: route-tree-drift through the turbo remote cache

**Files:**
- Modify: `.github/workflows/route-tree-drift.yml`

- [ ] **Step 1: Add the cache env to the `drift` job** (directly under `drift:`, before `runs-on:`), copied from js.yml's contract:

```yaml
    # Self-hosted turbo remote cache — same contract as js.yml: all three are
    # repository secrets, fork PRs see empty values and run uncached.
    env:
      TURBO_API: ${{ secrets.TURBO_API }}
      TURBO_TEAM: ${{ secrets.TURBO_TEAM }}
      TURBO_TOKEN: ${{ secrets.TURBO_TOKEN }}
      # The `secrets` context is not usable inside a step-level `if`; bridged
      # through env as a "true"/"false" string.
      TAILNET_ENABLED: ${{ secrets.TS_OAUTH_CLIENT_ID != '' }}
```

- [ ] **Step 2: Add the Tailscale + readiness steps** after `Install JS dependencies`, verbatim from js.yml (they are that file's `Tailscale` and `Wait for the cache host over the tailnet` steps):

```yaml
      # Join the tailnet as a tagged ephemeral node so turbo can reach the
      # remote cache at its tailnet address. Skipped when the OAuth secrets are
      # absent (fork PRs): turbo then runs local-only.
      - name: Tailscale
        if: env.TAILNET_ENABLED == 'true'
        uses: tailscale/github-action@v4
        with:
          oauth-client-id: ${{ secrets.TS_OAUTH_CLIENT_ID }}
          oauth-secret: ${{ secrets.TS_OAUTH_SECRET }}
          tags: tag:ci

      # Probe the cache endpoint before the first turbo invocation (any HTTP
      # status proves TCP works; normally 401 without a bearer). Non-fatal: a
      # dead cache host degrades to an uncached run, never a red one.
      - name: Wait for the cache host over the tailnet
        if: env.TAILNET_ENABLED == 'true'
        run: |
          host=$(printf '%s' "$TURBO_API" | sed -E 's#^[a-z]+://##; s#[:/].*$##')
          tailscale ping --until-direct=false --timeout 2s -c 10 "$host" > /dev/null 2>&1 || true
          for i in $(seq 1 10); do
            if code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 5 "$TURBO_API/v8/artifacts/status" 2>&1); then
              echo "cache endpoint answered HTTP $code"
              exit 0
            fi
            echo "attempt $i: $code"
            sleep 3
          done
          echo "::warning::turbo cache endpoint unreachable over the tailnet; this run is uncached"
```

- [ ] **Step 3: Replace the two build steps.** Delete `Build workspace packages` and `Regenerate route trees` (both steps and their comments) and put in their place:

```yaml
      # One turbo invocation builds the three apps and, via ^build, every
      # @bc-solutions-coder/* package they depend on — replacing the raw
      # `pnpm --filter` builds that bypassed the cache. A remote-cache hit is
      # sound for the drift check: each app's turbo.jsonc declares
      # routeTree.gen.ts as an OUTPUT and excludes it from inputs, so the task
      # hash covers exactly the route sources, and a restored tree is
      # byte-identical to a regenerated one.
      - name: Regenerate route trees
        run: >
          pnpm exec turbo run build
          --filter @bc-solutions-coder/wallow-auth
          --filter @bc-solutions-coder/wallow-web
          --filter @bc-solutions-coder/example-minimal-app
```

Keep the `Fail on route-tree drift` step and its developer-facing regeneration instructions unchanged — `pnpm --filter <app> build` remains the right local command.

- [ ] **Step 4: Verify.** `pnpm lint:actions` → green. Locally prove the turbo invocation regenerates trees: `pnpm exec turbo run build --filter @bc-solutions-coder/wallow-auth --filter @bc-solutions-coder/wallow-web --filter @bc-solutions-coder/example-minimal-app` then `git status` — route trees unchanged on an in-sync tree.

- [ ] **Step 5: Commit.**

```bash
git add .github/workflows/route-tree-drift.yml
git commit -m "perf(ci): route route-tree-drift builds through the turbo remote cache"
```

---

### Task 8: sdk-publish through the turbo remote cache

**Files:**
- Modify: `.github/workflows/sdk-publish.yml`

- [ ] **Step 1: Add the cache env to the `publish` job** (under `publish:`, before `defaults:`) — the same block as Task 7 Step 1 (TURBO_API / TURBO_TEAM / TURBO_TOKEN / TAILNET_ENABLED, comments included).

- [ ] **Step 2: Add the Tailscale + readiness steps** after `Install dependencies` — the same two steps as Task 7 Step 2, verbatim. (The job's `defaults.run.working-directory: packages/sdk` is harmless to the probe script and does not apply to `uses:` steps.)

- [ ] **Step 3: Replace the Test and Build steps.** Delete:

```yaml
      - name: Test
        run: pnpm test

      - name: Build
        run: pnpm build
```

and insert:

```yaml
      # Through turbo from the repo root (the job default cwd is packages/sdk,
      # where `pnpm build`/`pnpm test` run the package scripts directly and
      # bypass the cache — always cold). The --filter runs exactly the SDK's
      # build and test tasks; a cache hit restores the same dist/ that
      # `pnpm publish` packs below.
      - name: Build and test
        working-directory: ${{ github.workspace }}
        run: pnpm exec turbo run build test --filter @bc-solutions-coder/sdk
```

Keep the version-sync and publish steps untouched (they rely on the `packages/sdk` default cwd).

- [ ] **Step 4: Verify.** `pnpm lint:actions` → green. Locally: `pnpm exec turbo run build test --filter @bc-solutions-coder/sdk` from the repo root — green, and `packages/sdk/dist/` exists afterwards.

- [ ] **Step 5: Commit.**

```bash
git add .github/workflows/sdk-publish.yml
git commit -m "perf(ci): route sdk-publish build+test through the turbo remote cache"
```

---

### Task 9: Docs sync + closeout

**Files:**
- Modify: whichever docs the grep below surfaces (expected: `docs/operations/deployment.md`, `docs/getting-started/developer-guide.md`)
- Modify: `docs/plans/2026-08-28/1526-ci-caching-design.md`, `docs/plans/2026-08-28/1527-ci-caching-plan.md` (status lines)

- [ ] **Step 1: Read `docs/CLAUDE.md`** (docs rules) before editing any guide.

- [ ] **Step 2: Find stale descriptions:**

```bash
grep -rn -e 'route-tree' -e 'sdk-publish' -e 'CodeQL' -e 'codeql' -e 'build-v3' \
  -e 'actionlint' -e 'remote cache' -e 'paths-filter' docs/ --include='*.md' \
  | grep -v docs/plans/
```

Update every statement the Tasks 1–8 changes made false — the known candidates: the developer-guide's remote-cache section (which workflows use the cache now includes route-tree-drift and sdk-publish), and any deployment/CI narrative describing ci.yml's job graph or CodeQL triggering. Follow each doc's existing voice; no new pages.

- [ ] **Step 3: Flip both plan files** from `**status: active**` to `**status: completed**`. If Task 4 closed as no-op, note that beside the design doc's row for #2 instead of deleting it.

- [ ] **Step 4: Full gates.** `pnpm check` (package.json changed in Task 1; proves the JS side) and `pnpm lint:actions`. Backend untouched by Tasks 1–9 except workflow files, so no `./scripts/run-tests.sh` run is required — but if any earlier task touched anything under `api/`, run it.

- [ ] **Step 5: Commit.**

```bash
git add docs/
git commit -m "docs: sync ci descriptions with the caching changes"
```

---

## Bead Map (for spec-to-beads / team-build)

| Bead | Task | Depends on | Parallel-safe with |
|------|------|-----------|--------------------|
| B1 | Task 1 actionlint | — | B2 |
| B2 | Task 2 measurement | — | B1, B5–B8 |
| B3 | Task 3 ci.yml gating | B1 | B5, B6, B7, B8, B2 |
| B4 | Task 4 restore-mtime | B2 (verdict), B3 (same file) | B5–B8 |
| B5 | Task 5 codeql | B1 | B3, B4, B6–B8 |
| B6 | Task 6 openapi action | B1 | everything |
| B7 | Task 7 route-tree-drift | B1 | everything |
| B8 | Task 8 sdk-publish | B1 | everything |
| B9 | Task 9 docs + closeout | B1–B8 | — |

File-collision rule: B3 and B4 both edit `ci.yml` — the dependency serializes them. Every other bead owns its files exclusively.
