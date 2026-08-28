# Turborepo Adoption Implementation Plan

**status: completed**

> **Progress.** Phases 1 and 2 (Tasks 1–6) are **done and pushed** — turbo owns `build`,
> `typecheck`, `test` and `dev`, with local caching and per-branch `actions/cache` in CI. Warm
> `pnpm check` is ~13 s against a ~64 s baseline; measurements and the six cache-correctness
> verdicts are in `1206-turborepo-results.md`.
>
> **Phase 3 landed under bead `Wallow-m4u7`**, with one design change from what Tasks 7–11
> prescribe: the cache server is reached over a **Tailscale tailnet** (CI joins via
> `tailscale/github-action`, gated on the `TS_OAUTH_CLIENT_ID` secret), not a Cloudflare Tunnel
> hostname — turbo cannot attach the custom headers a fronting auth layer needs, and the public
> route rate-limited CI's artifact burst. `TURBO_API`/`TURBO_TEAM`/`TURBO_TOKEN` are repository
> secrets; the actions/cache fallback from Task 6 is deleted per Task 11. Verified remote-only:
> with the GitHub `turbo-*` caches wiped, both CI turbo invocations came back FULL TURBO from the
> tailnet server alone.
>
> Three corrections found while executing, recorded on the bead and in the results file: Task 3's
> `AUTH_BASE_PATH` grep probe is a false positive (`/auth/` matches backend API route strings in
> both builds — compare task hashes, or the `/auth/assets/*` asset prefix, instead); Task 10's
> claim that `futureFlags.longerSignatureKey` busts the global hash is wrong (it is hash-neutral);
> and `apps/examples/` no longer exists — see the note under Task 2, step 5.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task.
> Design: `docs/plans/2026-08-03/1206-turborepo-adoption-design.md`

**Goal:** Run `build`, `typecheck`, and `test` through `turbo` with content-addressed caching, then
back that cache with a self-hosted `turborepo-remote-cache` server exposed over a Cloudflare Tunnel
and shared by CI and developer machines.

**Architecture:** Turbo wraps only the three per-package fan-out scripts plus `dev`. The five
root-level tools (`oxlint`, `oxfmt`, `sherif`, `knip`, `check-exports.sh`) stay as plain root
scripts — see §1 of the design for why splitting them would fight `packages/lint/CLAUDE.md`. Cache
correctness rests on three things: `typecheck`/`test` declaring `dependsOn: ["^build"]`, each app
declaring its generated route tree as an output rather than an input, and `wallow-auth` declaring
`AUTH_BASE_PATH` in its `build.env`.

**Tech Stack:** turbo 2.x, pnpm 10.20.0 workspaces, `ducktors/turborepo-remote-cache` (Docker),
`cloudflared`, GitHub Actions.

**Rules that bind every task:**

- Conventional commits. Use `build:` for toolchain wiring, `ci:` for workflow edits, `docs:` for
  documentation. None of these release.
- Never add `CI` to `globalEnv` — it would give CI and laptops different hashes for identical
  inputs and defeat the shared cache.
- Every `inputs` array must begin with `$TURBO_DEFAULT$`. Declaring `inputs` opts the task out of
  `.gitignore`, so an array that doesn't start there will hash `node_modules`.
- A build-time variable goes in the owning task's `env`, never in `globalEnv`. `globalEnv` makes
  every member of the workspace miss cache when it changes; only one app reads `AUTH_BASE_PATH`.
- Nothing goes in `globalDependencies` unless it can change a `build`, `typecheck` or `test`
  output. Turbo runs none of the five root tools, so their configs do not belong there.
- Turbo hashes file **content**, not mtime. `touch` is never a valid cache-invalidation test.
- Read `packages/lint/CLAUDE.md` before touching any `.oxlintrc.json`. This plan does not touch one.
- After any root `package.json` or config edit, `pnpm lint:manifests && pnpm format:check` must
  still pass.

**Phases:** Tasks 1–7 are local and self-contained. Tasks 8–12 stand up the remote cache and need
access to the local server and the Cloudflare dashboard; they can be done days later without
blocking anything.

---

## Phase 0 — Baseline

### Task 1: Measure the cold cost of `pnpm check`

Every claim in the design is about time saved. Without this number there is nothing to compare
against, and no way to tell later whether the change was worth it.

**Files:**

- Create: `docs/plans/2026-08-03/1206-turborepo-results.md` (the measurement record; Task 5 appends
  to it and Task 12 leaves it alone)

**Step 1: Ensure a clean tree and a cold start**

```bash
git status --short          # expect no unstaged work you care about
rm -rf packages/*/dist apps/*/.output
```

**Step 2: Time a full check**

```bash
time pnpm check 2>&1 | tail -40
```

This takes several minutes. Record the wall-clock `real` figure.

**Step 3: Time the three fan-out tasks individually**

```bash
rm -rf packages/*/dist
time pnpm build
time pnpm typecheck
time pnpm test
```

**Step 4: Record the numbers**

Create `docs/plans/2026-08-03/1206-turborepo-results.md` with a `## Baseline` section holding all
four figures and the machine they were measured on. Measurements go in their own file rather than
appended to this one: `superpowers:executing-plans` walks this plan while the work proceeds, and a
plan that rewrites itself across three commits is a plan that keeps moving under the reader. Task 5
appends its verification results to the same results file; Task 12 marks this plan `completed`
without touching either set of numbers.

Note in that file which pnpm concurrency the baseline was taken at — `pnpm -r` defaults to
`workspace-concurrency` 4, and turbo defaults to 10. The comparison in Task 4 is not like-for-like
unless that is written down.

**Step 5: Commit**

```bash
git add docs/plans/2026-08-03/1206-turborepo-results.md
git commit -m "docs(plans): record pre-turbo check baseline"
```

---

## Phase 1 — Local caching

### Task 2: Install turbo and add the root configuration

**Files:**

- Modify: `package.json` (devDependencies, `format` and `format:check` scripts)
- Create: `turbo.jsonc`
- Modify: `.gitignore`
- Modify: `.lintstagedrc.mjs:62` (the JSON/YAML glob)

**Step 1: Install**

```bash
pnpm add -D -w turbo
pnpm exec turbo --version
```

**Step 2: Write `turbo.jsonc`**

`turbo.jsonc` (not `.json` — this repo comments its config, and `oxfmt` handles `.jsonc` including
comment preservation, verified):

```jsonc
{
  "$schema": "https://turborepo.dev/schema.json",
  // "stream" keeps CI logs greppable and linear. The interactive TUI would be
  // nicer locally, but a single mode means local and CI output match.
  "ui": "stream",
  // The root package.json, the lockfile, and the source of any internal package
  // the root depends on (here: @bc-solutions-coder/lint) are ALWAYS in the
  // global hash and must not be listed. These are the shared files turbo cannot
  // infer, and the list is deliberately short: every entry busts build,
  // typecheck and test across all 16 members.
  //
  // NOT listed, on purpose: .oxlintrc.json and .oxfmtrc.json. Turbo runs
  // neither oxlint nor oxfmt - those stay root scripts (design §1) - so hashing
  // them would invalidate the whole workspace for a lint-rule tweak that cannot
  // change a single build output.
  "globalDependencies": [
    "tsconfig.base.json",
    "tsconfig.build.base.json",
    ".nvmrc",
  ],
  // Deliberately NOT including CI: hashing it would give CI runners and laptops
  // different keys for identical inputs and defeat the shared remote cache. CI
  // is in turbo's built-in passthrough set, so tasks still see it.
  "globalEnv": ["NODE_ENV"],
  "tasks": {
    "build": {
      "dependsOn": ["^build"],
      "outputs": ["dist/**"],
    },
    // ^build, not nothing: every member resolves @bc-solutions-coder/* through
    // an exports map pointing at dist/, so a dependency must be BUILT before a
    // dependent can typecheck. It also folds the dependency's source into this
    // task's hash, which is what stops a stale pass being replayed.
    "typecheck": {
      "dependsOn": ["^build"],
    },
    "test": {
      "dependsOn": ["^build"],
    },
    // ^build here too, which the old `pnpm --parallel ... dev` could not
    // express: an app resolves its workspace dependencies through dist/, so
    // `pnpm dev` on a fresh clone dies with UNRESOLVED_IMPORT. A persistent
    // task may depend on others; it just may not be depended ON.
    "dev": {
      "dependsOn": ["^build"],
      "cache": false,
      "persistent": true,
    },
  },
}
```

**Step 3: Ignore the local cache**

Add to `.gitignore`, under the "Build outputs" block:

```gitignore
# Turborepo local cache and run scratch
.turbo/
```

**Step 4: Keep the formatter and lint-staged aware of the new file**

In `package.json`, append ` turbo.jsonc` to both the `format` and `format:check` script argument
lists, after `.oxfmtrc.json`.

In `.lintstagedrc.mjs:62`, widen the JSON glob so a staged `turbo.jsonc` is formatted:

```js
  "*.{json,jsonc,yml,yaml}": (files) => {
```

**Step 5: Verify turbo reads the workspace**

`pnpm-workspace.yaml` uses catalogs; confirm turbo resolves the graph rather than assuming it.

This step is what caught the `apps/examples/` problem. The workspace then negated `!apps/examples`
and re-included `apps/examples/*`, which pnpm honours but turbo applies as a directory-prefix
exclusion — so the nested app was silently absent from every turbo task. It was flattened to
`apps/minimal-app` (commit `a9cde094`) and the negation is gone. Expect **16 graph entries, 14 of
them buildable**; `config` and `lint` have no `build` script and are transit nodes.

```bash
pnpm exec turbo run build --dry=json | python3 -c "import json,sys; d=json.load(sys.stdin); print(len(d['tasks']), 'tasks'); [print(' ', t['taskId']) for t in d['tasks']]"
```

Expected: no error about the package manager or lockfile, and an entry for each of the **14**
packages that declare a `build` script — every member except `@bc-solutions-coder/config` and
`@bc-solutions-coder/lint`. Do not assert on the total count: turbo also emits graph entries for
Transit Nodes (packages pulled into the graph without the task), so the array can legitimately be
longer than 14. Check that the 14 buildable names are present, not that nothing else is.

**Step 6: Format and commit**

```bash
pnpm format && pnpm format:check && pnpm lint:manifests
git add package.json pnpm-lock.yaml turbo.jsonc .gitignore .lintstagedrc.mjs
git commit -m "build(turbo): add turborepo with the build/typecheck/test task graph"
```

---

### Task 3: Declare app build outputs and defuse the route-tree hazard

Each app's `vite build` writes `routeTree.gen.ts` into tracked source (see `route-tree-drift.yml`).
Left alone, the file is both an input and an output of the same task: a cold run mutates its own
input, and a warm run skips the build so the tree is never regenerated.

**Files:**

- Create: `apps/wallow-web/turbo.jsonc`
- Create: `apps/wallow-auth/turbo.jsonc`
- Create: `apps/minimal-app/turbo.jsonc`

No `package.json` edit is needed here. `oxfmt --write apps packages` already recurses into `apps/`
and formats `.jsonc` files it finds — verified. Only the *root* `turbo.jsonc` needs adding to the
two explicit root-file lists, which Task 2 Step 4 does.

**Step 1: Write the wallow-web configuration**

`apps/wallow-web/turbo.jsonc`:

```jsonc
{
  "extends": ["//"],
  "tasks": {
    "build": {
      // $TURBO_EXTENDS$ appends to the root's ["dist/**"] instead of replacing
      // it. .output is the deployable bundle; .nitro and .tanstack are scratch
      // and deliberately left uncached.
      //
      // routeTree.gen.ts is emitted BY this build into tracked source. Listing
      // it as an output means a cache hit restores it; excluding it from inputs
      // stops the task invalidating itself on every cold run. The tree stays
      // correct because its real sources - src/app/routes/** and vite.config.ts
      // - remain inputs.
      "outputs": ["$TURBO_EXTENDS$", ".output/**", "src/app/routeTree.gen.ts"],
      "inputs": ["$TURBO_DEFAULT$", "!src/app/routeTree.gen.ts"],
    },
  },
}
```

**Step 1b: Write the wallow-auth configuration**

`apps/wallow-auth/turbo.jsonc` — the same route-tree handling, plus the one build-time environment
variable in the repo:

```jsonc
{
  "extends": ["//"],
  "tasks": {
    "build": {
      "outputs": ["$TURBO_EXTENDS$", ".output/**", "src/app/routeTree.gen.ts"],
      "inputs": ["$TURBO_DEFAULT$", "!src/app/routeTree.gen.ts"],
      // vite.config.ts reads AUTH_BASE_PATH through process.env at config
      // evaluation and bakes the URL prefix into every emitted asset path (see
      // src/shared/lib/base-path.ts, docker-compose.production.yml's build-arg,
      // and docker/.env.production.example). Undeclared it fails twice: strict
      // envMode filters it out, so a based build silently emits a root-based
      // bundle; and it would be absent from the hash, so a /auth build and a /
      // build would share a cache key and be replayed for each other.
      //
      // Task-level, not globalEnv: wallow-web and minimal-app never read it and
      // must not miss cache when a fork changes the auth prefix.
      "env": ["AUTH_BASE_PATH"],
    },
  },
}
```

Verify the declaration actually bites, because a silently-ignored `env` key looks exactly like a
working one:

```bash
rm -rf apps/wallow-auth/.output
AUTH_BASE_PATH=/auth pnpm exec turbo run build --filter @bc-solutions-coder/wallow-auth
grep -rl '/auth/' apps/wallow-auth/.output | head        # prefix baked in somewhere

rm -rf apps/wallow-auth/.output
pnpm exec turbo run build --filter @bc-solutions-coder/wallow-auth   # no variable set
```

Expected: the second run is a cache **miss** — a different `AUTH_BASE_PATH` is a different hash —
and its output no longer carries the prefix. A hit means the `env` entry is not taking effect, and
the cache is unsafe for any fork that serves auth under a prefix. (`wallow-auth` is an SSR app with
no static `index.html`; the prefix lands in the server bundle and the asset manifest, so grep the
whole `.output` rather than one file.)

**Step 2: Write the minimal-app configuration**

`apps/minimal-app/turbo.jsonc` — same shape, but this app is still on the flat `src/`
layout, so the path is `src/routeTree.gen.ts`:

```jsonc
{
  "extends": ["//"],
  "tasks": {
    "build": {
      "outputs": ["$TURBO_EXTENDS$", ".output/**", "src/routeTree.gen.ts"],
      "inputs": ["$TURBO_DEFAULT$", "!src/routeTree.gen.ts"],
    },
  },
}
```

**Step 3: Prove the hazard is defused**

The failing condition is "second identical run is not a full cache hit". Check it directly:

```bash
pnpm exec turbo run build --filter @bc-solutions-coder/wallow-web
git status --short apps/wallow-web/src/app/routeTree.gen.ts   # expect: clean
pnpm exec turbo run build --filter @bc-solutions-coder/wallow-web
```

Expected on the second run: `cache hit, replaying logs` and a `FULL TURBO` summary line. If it
reports a miss, the exclusion is not taking effect — check the path matches the app's actual layout.

**Step 4: Prove a route change still regenerates**

A real content edit, not `touch` — turbo hashes content, so a mtime bump would report a hit and look
like a failure of the exclusion when it is really a failure of the test.

```bash
echo '// cache probe' >> apps/wallow-web/src/app/routes/index.tsx
pnpm exec turbo run build --filter @bc-solutions-coder/wallow-web
git checkout -- apps/wallow-web/src/app/routes/index.tsx
```

Expected: cache miss, task executes. This is the case `--affected` and stale caches both get wrong,
so do not skip it. Re-run the build after the revert so the working tree ends clean.

**Step 5: Commit**

```bash
pnpm format && pnpm format:check
git add apps/*/turbo.jsonc
git commit -m "build(turbo): declare app build outputs and exclude generated route trees from inputs"
```

---

### Task 4: Rewire the root scripts

**Files:**

- Modify: `package.json:9-12,22` (`dev`, `build`, `test`, `typecheck`, `check`)

**Step 1: Replace the four fan-out scripts**

```json
"dev": "turbo run dev --filter @bc-solutions-coder/wallow-web --filter @bc-solutions-coder/wallow-auth",
"build": "turbo run build",
"test": "turbo run test",
"typecheck": "turbo run typecheck",
```

**Step 2: Rewrite `check` as one DAG plus the root tools**

```json
"check": "pnpm format:check && pnpm lint && pnpm lint:tests && pnpm lint:manifests && pnpm lint:deps && turbo run build typecheck test && pnpm check:exports",
```

`build typecheck test` in a single `turbo run` is the point of the change — one graph, no phase
barriers. `check:exports` stays last and outside turbo: it needs every `dist/` present, and a root
task cannot express "after all packages built" (design §1).

**Step 3: Verify knip still resolves the toolchain**

```bash
pnpm lint:deps
```

`turbo` is a root devDependency invoked only from scripts. If knip reports it unused, add `"turbo"`
to `knip.json`'s `workspaces["."].ignoreDependencies` array, beside `@arethetypeswrong/cli` and
`publint`, and say why in the commit body.

**Step 4: Verify build-output parity under strict env mode**

Turbo 2.x defaults to `envMode: "strict"`, so tasks see only `globalEnv` plus built-in
passthroughs. The audit found exactly one build-time variable — `AUTH_BASE_PATH`, declared on
`wallow-auth`'s `build` task in Task 3 — and nothing else. Confirm that rather than trust it.

```bash
rm -rf packages/*/dist apps/*/.output
pnpm build
ls -la apps/wallow-web/.output/server/index.mjs packages/sdk/dist/index.js
```

Expected: both exist, build exits 0, and no warning about a missing environment variable. If a task
does need one, add it to that task's `env` array — not to `globalEnv`, which invalidates everything.

**Step 5: Run the full gate**

```bash
time pnpm check
```

Expected: passes. Record the cold figure in the results file against Task 1's baseline.

The two numbers are **not** like-for-like, and the cold run may well be *slower*. `pnpm -r` runs at
`workspace-concurrency` 4; turbo defaults to `concurrency: "10"`. Ten Vitest browser-mode suites
driving real headless Chromium at once — `ui`, `forms`, `navigation`, `auth`, `query`, `logger`,
both apps — is a plausible source of thrash on a laptop and of flake on a 2-core runner. If the cold
run regresses or a browser suite fails in a way it never does serially, set `"concurrency": "50%"`
in `turbo.jsonc` and re-measure before concluding anything. Record whichever value you settle on.

**Step 6: Run it again warm**

```bash
time pnpm check
```

Expected: `build`, `typecheck`, and `test` all report `FULL TURBO`. This is the headline number, and
it is the one that is comparable to the baseline — a cache hit is a cache hit at any concurrency.

**Step 7: Commit**

```bash
git add package.json knip.json
git commit -m "build(turbo): route build, typecheck, test and dev through turbo"
```

---

### Task 5: Verify cache correctness

The whole change is only safe if a stale cache cannot produce a false pass. Four adversarial checks
here, plus the `AUTH_BASE_PATH` check already run in Task 3.

**Files:** none (verification only — revert every edit made here)

**Step 1: A dependency's source change must invalidate its dependents' tests**

Probe an **existing tracked** file, never `>>` into a new path. `packages/utils` is subpath-only —
there is no `src/index.ts` — so `>>` would create an untracked file that `git checkout` then refuses
to revert (`did not match any file(s) known to git`), leaving the probe behind for knip to flag.

```bash
pnpm exec turbo run test                              # warm the cache
echo 'export const __cacheProbe = 1;' >> packages/utils/src/string.ts
pnpm exec turbo run test --filter @bc-solutions-coder/wallow-web
```

Expected: cache **miss**, the app's tests actually execute. A hit here means `^build` is not wired
and the cache is unsafe — stop and fix `turbo.jsonc` before going further.

```bash
git checkout -- packages/utils/src/string.ts
```

**Step 2: A failing test must not be cached as a pass**

```bash
pnpm exec turbo run test --filter @bc-solutions-coder/testing   # expect pass, cached
```

Introduce a deliberate failure in one assertion in `packages/testing/src/console-guard.test.ts`, then:

```bash
pnpm exec turbo run test --filter @bc-solutions-coder/testing   # expect FAIL
pnpm exec turbo run test --filter @bc-solutions-coder/testing   # expect FAIL again, not a hit
git checkout -- packages/testing/src/console-guard.test.ts
```

Turbo does not cache failures, but confirm it here rather than assume it.

**Step 3: A shared config change must invalidate everything downstream**

Turbo hashes file **content**, not mtime, so `touch` changes nothing and would report a hit — which
reads as a failure of `globalDependencies` when it is really a failure of the test. Make a real
edit.

```bash
pnpm exec turbo run typecheck                        # warm
# real content change: reorder an array or add a genuinely new key
$EDITOR tsconfig.base.json
pnpm exec turbo run typecheck                        # expect a FULL miss, every member
git checkout -- tsconfig.base.json
```

`tsconfig.base.json` is in `globalDependencies`, so it busts the global hash and nothing downstream
can hit. While you are here, confirm the negative case too: edit `.oxlintrc.json` trivially and
expect typecheck to be a **full hit**, because Task 2 deliberately left it out of
`globalDependencies`.

**Step 4: A Vite preset change must invalidate its consumers**

`packages/config` has no `build` script but is a declared dependency of 15 members. This is the
Transit Node check: turbo should still fold its files into every consumer's hash even though it
executes nothing for it.

```bash
pnpm exec turbo run build                            # warm
echo '// cache probe' >> packages/config/src/vite/library.ts
pnpm exec turbo run build
git checkout -- packages/config/src/vite/library.ts
```

Expected: broad misses across the workspace. Read the run summary for the miss count rather than
grepping `--dry=json` for `"cache miss"` — that string does not appear in dry-run output (the field
is `"cache": {"status": "MISS"}`), so the grep can only ever return 0 and would pass silently.

**Step 5: Record the results**

Append a `## Cache-correctness verification` section to
`docs/plans/2026-08-03/1206-turborepo-results.md` (created in Task 1) noting each of the five checks
— including Task 3's `AUTH_BASE_PATH` check — and its outcome. Commit that alone:

```bash
git add docs/plans/2026-08-03/1206-turborepo-results.md
git commit -m "docs(plans): record turbo cache-correctness verification results"
```

---

## Phase 2 — CI, local cache only

### Task 6: Rewrite the JS workflow around turbo

**Files:**

- Modify: `.github/workflows/js.yml`

**Step 0: Make the workflow trigger on its own task graph**

`js.yml`'s `paths:` filters list every root config that can change a build — `tsconfig.base.json`,
`.oxlintrc.json`, `.nvmrc`, `.npmrc` — but they predate turbo. Editing the root task graph would
change how every task runs and trigger no CI at all.

Add `"turbo.jsonc"` to **both** the `pull_request.paths` and the `push.paths` lists, beside
`"tsconfig.base.json"`. The three package configurations need no entry: `apps/**` already covers
them.

**Step 1: Add the cache restore step**

Immediately after "Install dependencies" (`js.yml:57-58`):

```yaml
      # Phase-3 fallback until the self-hosted remote cache is live. Per-branch
      # and less effective than a shared cache, but it turns a same-branch push
      # into a mostly-cached run.
      - name: Restore turbo cache
        uses: actions/cache@v4
        with:
          path: .turbo
          key: turbo-${{ runner.os }}-${{ github.sha }}
          restore-keys: turbo-${{ runner.os }}-
```

**Step 2: Delete the standalone build-packages step**

Remove `js.yml:81-87` — the comment block and the `pnpm --filter './packages/*' build` step. Its
job is now `typecheck`'s `dependsOn: ["^build"]`. Move the essence of that comment into the "Build,
typecheck and test" step added below, so the reason survives.

**Step 3: Collapse the three phases into one turbo run**

Replace the separate `Typecheck` (`js.yml:95-96`), `Test` (`js.yml:106-107`) and `Build`
(`js.yml:109-110`) steps with a single step placed after the Playwright browser install:

```yaml
      # One task graph, not three phases. Each member resolves
      # @bc-solutions-coder/* through an exports map pointing at dist/, which is
      # why typecheck and test both declare dependsOn ["^build"] in turbo.jsonc
      # - a dependency is always built before a dependent reads its .d.ts files.
      # Every task runs on every PR: unchanged packages restore from cache, so
      # the merge commit keeps the guarantee that everything actually passed.
      - name: Build, typecheck and test
        run: pnpm exec turbo run build typecheck test
```

**Step 4: Reorder `check:exports`**

The "Check package export surface" step (`js.yml:92-93`) currently sits after the deleted build
step and needs `dist/` to exist. Move it to **after** the new combined step.

**Step 5: Validate the workflow parses**

```bash
python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/js.yml')); print('ok')"
```

**Step 6: Commit and push to a branch to watch it run**

```bash
git add .github/workflows/js.yml
git commit -m "ci(js): run build, typecheck and test as one turbo task graph"
```

**Step 7: Confirm the second CI run is faster — and watch for browser-test flake**

Push an empty commit to the same branch and compare job durations. The second run should show cache
hits for every unchanged package.

Read the *first* run's log carefully before celebrating the second. `ubuntu-latest` is a small
runner and turbo's default `concurrency: "10"` will start far more Vitest browser-mode suites at
once than the old sequential `pnpm test` ever did. A timeout or a flaky assertion in `ui`, `forms`,
`navigation` or either app on the cold run is a contention symptom, not a real failure: set
`"concurrency": "50%"` in `turbo.jsonc` and re-run before investigating the spec.

---

## Phase 3 — Self-hosted remote cache

> Everything below runs on the local server and in the Cloudflare dashboard. Nothing in Phase 1 or 2
> depends on it.

### Task 7: Generate the two secrets

**Files:** none (secrets only — never commit these)

**Step 1: Generate a cache token and a signature key**

```bash
openssl rand -hex 32   # -> TURBO_TOKEN
openssl rand -hex 32   # -> TURBO_REMOTE_CACHE_SIGNATURE_KEY (64 hex chars = 32 bytes)
```

The signature key must be at least 32 bytes; `futureFlags.longerSignatureKey` (Task 10) makes a
short one a hard error rather than a silently weakened HMAC.

**Step 2: Store them**

Put both in the password manager. They will be needed in three places: the server's `.env`, GitHub
Actions secrets, and each developer's shell.

---

### Task 8: Stand up the cache server

**Files:**

- Create: `docker/turbo-cache/docker-compose.yml`
- Create: `docker/turbo-cache/.env.example`

No `.gitignore` change: the existing bare `.env` rule has no slash, so git applies it at every
depth and `docker/turbo-cache/.env` is already ignored. Confirm with
`git check-ignore -v docker/turbo-cache/.env` rather than adding a redundant rule.

Committing the compose file is deliberate: it is the deployable definition, and a fork inherits a
working cache server rather than a paragraph telling them to build one.

**Step 1: Write the compose file**

`docker/turbo-cache/docker-compose.yml`:

```yaml
# Self-hosted Turborepo remote cache, plus the Cloudflare Tunnel that publishes
# it. Runs on the build server, NOT as part of `pnpm backend:infra` - it is
# developer infrastructure, unrelated to the Wallow stack.
#
# The tunnel dials out, so no inbound port is opened. Authentication is
# TURBO_TOKEN alone: turbo sends only `Authorization: Bearer`, so Cloudflare
# Access service tokens cannot be layered on top (see the design doc). Pair this
# with a WAF rule on the hostname.
services:
  turbo-cache:
    image: ducktors/turborepo-remote-cache:latest
    restart: unless-stopped
    environment:
      PORT: "3000"
      TURBO_TOKEN: ${TURBO_TOKEN:?set TURBO_TOKEN in docker/turbo-cache/.env}
      TURBO_REMOTE_CACHE_SIGNATURE_KEY: ${TURBO_REMOTE_CACHE_SIGNATURE_KEY:?set in .env}
      STORAGE_PROVIDER: local
      STORAGE_PATH: /cache
      # Default is true, which would prefix STORAGE_PATH with the system tmp
      # dir - and lose the whole cache on reboot.
      STORAGE_PATH_USE_TMP_FOLDER: "false"
      LOG_LEVEL: info
      ENABLE_STATUS_LOG: "false"
    volumes:
      - turbo-cache-data:/cache
    expose:
      - "3000"

  cloudflared:
    image: cloudflare/cloudflared:latest
    restart: unless-stopped
    command: tunnel --no-autoupdate run
    environment:
      TUNNEL_TOKEN: ${CLOUDFLARE_TUNNEL_TOKEN:?create the tunnel first, see the plan}
    depends_on:
      - turbo-cache

volumes:
  turbo-cache-data:
```

**Step 2: Write the example env file**

`docker/turbo-cache/.env.example`:

```dotenv
# openssl rand -hex 32
TURBO_TOKEN=
# openssl rand -hex 32 (must decode to >= 32 bytes)
TURBO_REMOTE_CACHE_SIGNATURE_KEY=
# From the Cloudflare Zero Trust dashboard when you create the tunnel
CLOUDFLARE_TUNNEL_TOKEN=
```

**Step 3: Pin the image**

`:latest` is a starting point, not a resting place. After the first successful run, replace both
tags with the digest actually pulled:

```bash
docker image inspect ducktors/turborepo-remote-cache:latest --format '{{index .RepoDigests 0}}'
```

**Step 4: Bring up the cache alone and smoke-test it locally**

On the server, before any tunnel exists:

```bash
cd docker/turbo-cache
cp .env.example .env      # fill in TURBO_TOKEN and the signature key
docker compose up -d turbo-cache
docker compose logs -f turbo-cache      # expect "server listening"
```

Then from the server itself:

```bash
docker compose exec turbo-cache node -e "fetch('http://localhost:3000/v8/artifacts/status',{headers:{Authorization:'Bearer '+process.env.TURBO_TOKEN}}).then(r=>r.text()).then(console.log)"
```

Expected: `{"status":"enabled"}`. A 401 means the token does not match; a connection error means the
server is not up.

**Step 5: Commit**

```bash
git add docker/turbo-cache/docker-compose.yml docker/turbo-cache/.env.example
git commit -m "build(turbo): add self-hosted remote cache compose stack"
```

---

### Task 9: Publish it through a Cloudflare Tunnel

**Files:** none in the repo (Cloudflare dashboard + server)

**Step 1: Create the tunnel**

In the Cloudflare **Zero Trust** dashboard → **Networks** → **Tunnels** → **Create a tunnel** →
**Cloudflared**. Name it `wallow-turbo-cache`. Copy the tunnel token it shows.

**Step 2: Put the token in the server's `.env`**

Set `CLOUDFLARE_TUNNEL_TOKEN` in `docker/turbo-cache/.env`, then:

```bash
docker compose up -d
docker compose logs -f cloudflared     # expect "Registered tunnel connection" x4
```

**Step 3: Add the public hostname**

Still in the tunnel's configuration, **Public Hostname** → **Add a public hostname**:

- Subdomain: `turbo-cache`
- Domain: your zone
- Service type: `HTTP`
- URL: `turbo-cache:3000` — the compose service name, because `cloudflared` resolves it on the
  compose network.

**Step 4: Verify from outside the network**

From a machine that is not on the server's LAN — a phone tether works:

```bash
curl -s -H "Authorization: Bearer $TURBO_TOKEN" \
  https://turbo-cache.<your-zone>/v8/artifacts/status
```

Expected: `{"status":"enabled"}`. Also confirm the unauthenticated case is refused:

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://turbo-cache.<your-zone>/v8/artifacts/status
```

Expected: `401`. If this returns `200`, `TURBO_TOKEN` did not reach the container and the cache is
open to the internet — tear the hostname down before continuing.

**Step 5: Add the WAF rule**

The hostname is publicly resolvable and turbo cannot send Cloudflare Access headers, so the token is
the only credential. Add a second layer in **Security** → **WAF** → **Custom rules**:

- Expression: hostname equals `turbo-cache.<your-zone>` **and** `ip.src` not in the GitHub Actions
  egress ranges **and** `ip.src` not in your own network.
- Action: **Block**.

Fetch the current Actions ranges from `https://api.github.com/meta` (the `actions` array). These
change; note in the rule description that it needs periodic review. This narrows exposure to "every
GitHub Actions customer plus you", which is meaningfully better than "the internet" and no
substitute for a strong token.

Optionally add a rate-limiting rule on the same hostname.

---

### Task 10: Point turbo at the remote cache

**Files:**

- Modify: `turbo.jsonc`

**Step 1: Add the remote cache block**

Add to `turbo.jsonc`, as a sibling of `tasks`:

```jsonc
  "futureFlags": {
    // Reject a TURBO_REMOTE_CACHE_SIGNATURE_KEY shorter than 32 bytes outright,
    // instead of silently accepting a weakened HMAC-SHA256.
    "longerSignatureKey": true,
  },
  "remoteCache": {
    "apiUrl": "https://turbo-cache.<your-zone>",
    // Integrity, not access control: catches truncated or corrupted artifacts.
    // It does nothing against someone who already holds TURBO_TOKEN.
    "signature": true,
    "timeout": 60,
    "uploadTimeout": 120,
  },
```

`apiUrl` is the bare origin — turbo appends `/v8/artifacts/...` itself.

Note that adding `futureFlags` changes the global hash, so the next run is a full miss everywhere.
That is expected and happens once.

**Step 2: Set the client credentials locally**

In `~/.zshrc` (not in the repo):

```bash
export TURBO_TOKEN=<the token from Task 7>
export TURBO_TEAM=team_wallow
export TURBO_REMOTE_CACHE_SIGNATURE_KEY=<the signature key from Task 7>
```

`TURBO_TEAM` namespaces the artifacts on the server. Prefixing with `team_` keeps it valid whether
turbo treats it as a slug or an ID.

**Step 3: Prove upload and download**

```bash
source ~/.zshrc
rm -rf .turbo packages/*/dist
pnpm exec turbo run build            # expect misses, then "Remote caching enabled"
rm -rf .turbo packages/*/dist
pnpm exec turbo run build            # expect cache hits sourced from REMOTE
```

The second run has no local cache at all, so any hit must have come over the tunnel. If it misses,
check `docker compose logs turbo-cache` on the server for 4xx responses.

**Step 4: Confirm artifacts landed on the server**

```bash
docker compose exec turbo-cache sh -c 'ls -la /cache && du -sh /cache'
```

**Step 5: Commit**

```bash
pnpm format && pnpm format:check
git add turbo.jsonc
git commit -m "build(turbo): enable the self-hosted remote cache with signature verification"
```

---

### Task 11: Wire CI to the remote cache

**Files:**

- Modify: `.github/workflows/js.yml`

**Step 1: Add the repository secrets and variable**

```bash
gh secret set TURBO_TOKEN
gh secret set TURBO_REMOTE_CACHE_SIGNATURE_KEY
gh variable set TURBO_TEAM --body team_wallow
```

**Step 2: Expose them to the job**

At the `build` job level in `js.yml`, above `runs-on`:

```yaml
    env:
      TURBO_TOKEN: ${{ secrets.TURBO_TOKEN }}
      TURBO_TEAM: ${{ vars.TURBO_TEAM }}
      TURBO_REMOTE_CACHE_SIGNATURE_KEY: ${{ secrets.TURBO_REMOTE_CACHE_SIGNATURE_KEY }}
```

**Step 3: Decide on forks**

Secrets are not exposed to `pull_request` runs from forks, so those jobs run with no remote cache
and simply execute everything — correct, just slower. Leave it. Do **not** switch to
`pull_request_target` to work around it.

**Step 4: Remove the `actions/cache` fallback**

Delete the "Restore turbo cache" step added in Task 6. The remote cache supersedes it, and keeping
both means uploading the same artifacts twice.

**Step 5: Verify a cross-machine hit**

Build a branch locally so its artifacts are in the remote cache, push it, and confirm CI reports
cache hits for the packages you did not touch. This is the payoff of the whole phase — if it does
not happen, compare the `NODE_ENV` and `globalEnv` values between the two environments, since a
hash divergence is almost always an environment variable.

**Step 6: Commit**

```bash
git add .github/workflows/js.yml
git commit -m "ci(js): use the self-hosted turbo remote cache"
```

---

## Phase 4 — Documentation

### Task 12: Document the toolchain change

**Files:**

- Modify: `CLAUDE.md` (the JavaScript / TypeScript Monorepo command block)
- Modify: `docs/getting-started/developer-guide.md`
- Modify: `docs/plans/2026-08-03/1206-turborepo-adoption-design.md` (status line)
- Modify: this file (status line)

`1206-turborepo-results.md` is not touched here — it is the measurement record and stays as written.

**Step 1: Update the root CLAUDE.md command block**

`pnpm build`, `pnpm test`, `pnpm typecheck` are no longer `pnpm -r` — correct the inline comments,
and add one line stating that turbo owns those three plus `dev`, while lint/format/manifests/deps/
exports remain root invocations. Keep it to two or three lines: this file is paid for by every
session.

**Step 2: Document the remote cache for developers**

Add a section to `docs/getting-started/developer-guide.md` covering the three environment variables
a developer needs, where to get them, and the two escape hatches when the cache misbehaves:
`turbo run <task> --force` to bypass, and `TURBO_REMOTE_CACHE_ENABLED=false`… — verify the exact
opt-out name against the docs before writing it, and prefer documenting
`"remoteCache": { "enabled": false }` if no clean env override exists.

Read `docs/CLAUDE.md` before writing, per repo rules.

**Step 3: Mark both plan documents completed**

Change `**status: active**` to `**status: completed**` in this file and in the design doc.

**Step 4: Run the full gate one final time**

```bash
pnpm check
```

**Step 5: Commit and finish the session properly**

```bash
git add CLAUDE.md docs/
git commit -m "docs: document the turborepo toolchain and self-hosted remote cache"
git pull --rebase && bd dolt push && git push
git ls-remote origin refs/dolt/data
```

---

## Open items for later

- **Cache eviction.** `ducktors/turborepo-remote-cache` has no built-in GC. Once the volume's growth
  rate is known, add a cron on the server (`find /cache -type f -atime +30 -delete`) or call its
  `POST /clean` endpoint. Turbo's `cacheMaxAge` / `cacheMaxSize` settings govern only the *local*
  `.turbo` directory.
- **`--affected`.** Deliberately not adopted (design §4). Revisit only after the `inputs`/`outputs`
  globs have gone a few months without a stale-cache incident, and pair it with
  `futureFlags.githubActionsRemoteBaseRefFallback`, which `actions/checkout`'s detached checkouts
  require.
- **S3 storage.** If the local volume becomes the constraint, `STORAGE_PROVIDER=s3` points the
  server at any S3-compatible endpoint — the repo already runs GarageHQ for the Storage module.
- **`turbo boundaries`.** Could eventually enforce the dependency rules that
  `packages/utils`/`packages/env` state in prose ("bottom of the graph, zero dependencies"). Out of
  scope here.
