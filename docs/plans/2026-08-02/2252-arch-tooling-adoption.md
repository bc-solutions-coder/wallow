# Architecture Tooling Adoption Implementation Plan

**status: completed** — all three phases landed on `chore/arch-tooling-adoption` (2026-08-03).
Deviations from plan: sherif needed zero ignores; knip's exports category was adopted rather
than scoped out (so no bead for it); Task 8 Step 1's documented failure did not reproduce on
oxlint 1.74.0 as installed (see `Wallow-4ip4` and the Task 8 commit message), so the mirror
relocation landed as robustness rather than as an unblock; the snippet scratch directory stayed
in `os.tmpdir()` deliberately.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task.

**Goal:** Close the three enforcement gaps named in
`docs/plans/2026-08-02/2212-arch-rules-review.md` — manifest shape (sherif), dead code/undeclared
deps (knip), and the 11-package `wallow/*` coverage hole (root `jsPlugins` registration) — so
architecture violations fail `pnpm check` in seconds without any structure tests.

**Architecture:** Three independent phases, cheapest first. Phases 1–2 add two zero/low-config
CLIs to the root gate *before* the build step. Phase 3 reworks
`packages/sdk/src/oxlint-guardrails.test.ts` to stop copying the root config outside the repo,
which is the sole blocker (`packages/lint/CLAUDE.md` "Why the root config is off-limits") to
registering `@bc-solutions-coder/lint` once at the root. A `package-dag` oxlint rule is
deliberately **out of scope** — filed as a bead in Task 12, adopted only if glob-based
`no-restricted-imports` proves insufficient.

**Tech Stack:** sherif ^1.13 (Rust, zero-config manifest linter), knip ^6.31 (dead-code and
dependency analysis, native pnpm-workspace support), oxlint (already ^1.74 at root).

**Verification currency note:** every external-tool fact here (sherif's rule list and flags,
knip's config keys, oxlint `jsPlugins` resolution) was researched 2026-08-02 but MUST be
re-verified against the installed version's docs via `mcp__ref-context__ref_search_documentation`
before writing config — repo rule (`CLAUDE.md` "External library docs — use ref.tools").

---

## Phase 1 — sherif (manifest hygiene)

### Task 1: Triage run

**Step 1:** Run sherif without installing anything:

```bash
pnpm dlx sherif@latest
```

Expected: a report (or clean exit 0). Its ten rules: `empty-dependencies`,
`multiple-dependency-versions`, `unsync-similar-dependencies`, `root-package-manager-field`,
`root-package-private-field`, `types-in-dependencies`, `unordered-dependencies` (errors);
`non-existant-packages`, `packages-without-package-json`, `root-package-dependencies` (warnings).
Source: https://github.com/QuiiBz/sherif

**Step 2:** For each finding, decide fix vs ignore — **fix is the default**. Legitimate ignores
(via `--ignore-rule <name>` / `--ignore-package <name>` appended to the script in Task 2) need a
reason recorded in the root `CLAUDE.md` edit in Task 3. Known likely findings to expect:
`multiple-dependency-versions` across workspace devDeps, and `unordered-dependencies` if any
manifest isn't alphabetized. Do NOT auto-run `--fix` on the first pass — review first, because
`--fix` re-runs install.

**Step 3:** Apply the chosen fixes (edit the offending `package.json` files, or run
`pnpm dlx sherif@latest --fix` once the diff is understood). Then `pnpm install` if manifests
changed, and rerun until exit 0.

**Step 4:** Commit:

```bash
git add -A && git commit -m "chore(tooling): align workspace manifests for sherif adoption"
```

(Skip the commit if the triage run was already clean.)

### Task 2: Wire sherif into the gate

**Files:**

- Modify: `package.json` (root — scripts + devDependencies)
- Modify: `.github/workflows/js.yml`

**Step 1:** Install pinned at root:

```bash
pnpm add -D -w sherif
```

**Step 2:** Add the script and splice it into `check` immediately after `lint:tests` (fails in
milliseconds, needs no `node_modules` state, so it belongs before the expensive steps):

```json
"lint:manifests": "sherif",
"check": "pnpm format:check && pnpm lint && pnpm lint:tests && pnpm lint:manifests && pnpm build && pnpm typecheck && pnpm test && pnpm check:exports",
```

Append any `--ignore-rule`/`--ignore-package` flags decided in Task 1 to the `lint:manifests`
script string — sherif has no config file; the script IS the config.

**Step 3:** Add a CI step to `.github/workflows/js.yml` right after the `Lint tests` step
(`js.yml:65-66`):

```yaml
      - name: Lint manifests
        run: pnpm lint:manifests
```

**Step 4:** Verify locally:

```bash
pnpm lint:manifests && pnpm check
```

Expected: both exit 0.

**Step 5:** Commit:

```bash
git add package.json pnpm-lock.yaml .github/workflows/js.yml
git commit -m "chore(tooling): add sherif manifest lint to the quality gate"
```

### Task 3: Document

**Files:**

- Modify: `CLAUDE.md` (root — the "JavaScript / TypeScript Monorepo" command block: add
  `pnpm lint:manifests`, and note WHY each ignore flag from Task 1 exists, if any)

**Step 1:** Make the edit — one line in the command block, one sentence per ignore. Keep it
minimal; this file is paid for by every session.

**Step 2:** Commit: `git commit -m "docs: document the sherif manifest gate"`

---

## Phase 2 — knip (unused files / exports / dependencies)

### Task 4: Baseline run and placement decision

**Step 1:** Install and run with no config:

```bash
pnpm add -D -w knip
pnpm exec knip
```

knip reads `pnpm-workspace.yaml` natively; once workspaces are detected, root-level
`entry`/`project` defaults apply per workspace. Source:
https://knip.dev/features/monorepos-and-workspaces

**Step 2:** Record two observations before touching config:

- Whether workspace-package imports resolve **without** `dist/` built (`git clean -dfx` NOT
  required — just note whether `unresolved` errors mention `dist`). This decides gate placement:
  if knip needs built packages, it goes **after** `pnpm build` in `check`; otherwise after
  `lint:manifests`. Do not guess — decide from this run's output.
- The finding counts per category (files / dependencies / exports / unlisted).

### Task 5: Author `knip.json`

**Files:**

- Create: `knip.json` (repo root)

**Step 1:** Verify current config-key names via ref.tools
(`knip workspaces entry project ignoreDependencies`), then write the config. Starting shape —
adjust entries to what Task 4's output showed:

```json
{
  "$schema": "https://unpkg.com/knip@6/schema.json",
  "workspaces": {
    "apps/*": {},
    "apps/examples/*": {},
    "packages/*": {}
  },
  "ignore": ["packages/sdk/src/generated/**", "packages/lint/fixtures/**"],
  "ignoreDependencies": []
}
```

Rules for the triage that follows:

- **`packages/sdk/src/generated/**` and `packages/lint/fixtures/**` stay ignored** — generated
  code and deliberate-violation fixtures.
- **Unused-exports findings on package entry files are NOT expected**: knip's
  `includeEntryExports` defaults to false, so a fork-facing public API exported from an
  `exports`-map entry is not reported. If exports findings still swamp the run, scope the first
  adoption to files+dependencies only and file a bead for the exports category rather than
  ignore-listing en masse.
- Every `ignoreDependencies` entry needs a trailing reason in the PR description; if the reason
  is durable, it goes in the Task 7 docs edit.

**Step 2:** Iterate `pnpm exec knip` → fix or configure → rerun, until exit 0. Genuine dead code
gets **deleted** (that is the point); genuinely-used-but-undetected things get a plugin/entry
config fix, not an ignore, wherever possible.

**Step 3:** Commit in two pieces so review is possible:

```bash
git add knip.json package.json pnpm-lock.yaml
git commit -m "chore(tooling): add knip configuration"
# then, if dead code was deleted:
git add -A && git commit -m "refactor: remove dead code surfaced by knip baseline"
```

### Task 6: Wire knip into the gate

**Files:**

- Modify: `package.json` (root), `.github/workflows/js.yml`

**Step 1:** Add script + splice into `check` at the position Task 4 decided:

```json
"lint:deps": "knip",
```

**Step 2:** Add the CI step to `js.yml` at the matching position (after `Lint manifests`, or
after `Build packages` at `js.yml:76-77` if dist is required).

**Step 3:** `pnpm check` — expected exit 0.

**Step 4:** Commit: `git commit -m "chore(tooling): add knip to the quality gate"`

### Task 7: Document

Same shape as Task 3: command-block line in root `CLAUDE.md`, durable ignore reasons, commit
`docs: document the knip gate`.

---

## Phase 3 — root `jsPlugins` registration (closes the 11-package hole)

**Read first:** `packages/lint/CLAUDE.md` in full — especially "Why the root config is off-limits"
and "The cost of nesting". The constraint being removed was measured on oxlint 1.74.0; re-verify
resolution behaviour on the installed version before concluding anything
(https://oxc.rs/docs/guide/usage/linter/nested-config).

### Task 8: Rework the guardrails spec's mirror-tree location (TDD — the spec is the test)

**Files:**

- Modify: `packages/sdk/src/oxlint-guardrails.test.ts` (the `mkdtempSync(tmpdir())` sites and
  `lintMirrorTree` — read the whole spec's comments before editing; it documents its own
  technique)
- Modify: `.gitignore` (root), `.oxlintrc.json` `ignorePatterns` (`.oxlintrc.json:112`), and the
  root `format`/`format:check` scripts only if the new location isn't already excluded

**The change:** the spec currently copies the root config into `mkdtempSync(join(tmpdir(), …))` —
a directory outside the repo with no reachable `node_modules`, which is what makes any root
`jsPlugins` entry fatal to it (a `jsPlugins` specifier resolves from the config file's own
directory). Move the mirror root **inside the repo**: `mkdtempSync` under a gitignored
`.lint-mirror/` directory at the repo root. From there, Node's walk-up resolution reaches the
root `node_modules`, so a bare `@bc-solutions-coder/lint` specifier in the copied config loads.

**Step 1:** Prove the failure mode first. Add a THROWAWAY `jsPlugins` entry to the root
`.oxlintrc.json`:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }]
```

Run: `pnpm --filter @bc-solutions-coder/sdk test -- oxlint-guardrails`
Expected: the suite fails to collect / 0 tests run (the documented failure). If it instead
passes, the constraint no longer exists on this oxlint version — record that in the commit
message, revert the throwaway entry, skip Step 2's relocation rationale but still do Steps 2–4
(the in-repo location is still more robust), and simplify Task 9 accordingly.

**Step 2:** Revert the throwaway entry. Edit the spec: replace the OS-tmpdir base with
`<repoRoot>/.lint-mirror/`, `mkdirSync`'d if absent, keeping `mkdtempSync`'s per-run uniqueness
and the existing `rmSync` cleanup in `afterAll`. Update the spec's header comment — it currently
explains the temp-dir technique; it must now explain WHY the mirror lives in-repo (jsPlugins
resolution) or the next reader will "clean it up" back to `tmpdir()`.

**Step 3:** Exclude the directory everywhere it could leak:

- `.gitignore`: add `.lint-mirror/`
- Root `.oxlintrc.json` `ignorePatterns`: add `.lint-mirror` (the mirror contains deliberate
  violations; `pnpm lint`'s roots are `apps packages` so this is belt-and-braces, but a future
  root-widening must not trip on it)

**Step 4:** Run: `pnpm --filter @bc-solutions-coder/sdk test -- oxlint-guardrails`
Expected: PASS, same test count as before the edit (compare against a pre-edit run).

**Step 5:** With the spec green and NO root jsPlugins yet, re-add the throwaway entry and rerun.
Expected: PASS this time — this is the proof the rework achieved its purpose. Remove the entry
again (it lands for real in Task 9).

**Step 6:** Commit:

```bash
git add packages/sdk/src/oxlint-guardrails.test.ts .gitignore .oxlintrc.json
git commit -m "test(sdk): move the oxlint guardrail mirror tree inside the repo"
```

### Task 9: Register the plugin at the root; enable `no-source-tests` repo-wide

**Files:**

- Modify: `.oxlintrc.json` (root) — add the `jsPlugins` entry (JSONC comment beside it naming
  Task 8's spec as the reason this is now possible) and `"wallow/no-source-tests": "error"` in
  the root `rules`
- Modify: `package.json` (root) — add `"@bc-solutions-coder/lint": "workspace:*"` to
  devDependencies (the specifier resolves from the config's directory — the root — so the root
  must depend on it), then `pnpm install`

**Deliberately NOT in this task:** enabling the other five `wallow/*` rules at the root. Their
options are per-tree (`text-heading-variant` differs between the apps; `zone-dag` is inert
without tsconfig paths) and an oxlint override REPLACES options rather than merging (measured —
see bd memory). The five nested configs keep their registration AND enablement untouched;
redundant registration is harmless and removing it is a separate decision after this soaks.

**Step 1:** Make both edits. **Step 2:** Run the full local gate:

```bash
pnpm lint && pnpm lint:tests
```

Expected: both exit 0 — `no-source-tests` self-gates on `*.test.*` filenames and bans `node:fs`
imports there. The nine known node:fs specs are in the review doc §3: the two tool-output
guardrails (`packages/lint/src/fixtures.test.ts`, `packages/sdk/src/oxlint-guardrails.test.ts`)
and the artifact readers (sdk openapi/query-surface, styles theme-css/assets, query identity,
testing browser-styles-wiring). **Decision gate:** these are doctrine-blessed
(`.claude/rules/TESTING.md` names the exemption classes). If the rule now fires on them, add ONE
scoped override block in the root config exempting exactly those files by path, each with a JSONC
comment naming its doctrine class — do NOT weaken the rule or re-scope it to the five trees.

**Step 3:** Run the two rule-consuming suites:

```bash
pnpm --filter @bc-solutions-coder/lint test
pnpm --filter @bc-solutions-coder/sdk test -- oxlint-guardrails
```

Expected: PASS. **Step 4:** Full `pnpm check`. Expected: exit 0.

**Step 5:** Commit:

```bash
git add .oxlintrc.json package.json pnpm-lock.yaml
git commit -m "chore(lint): register the wallow plugin at the repo root"
```

### Task 10: Update the lint package's own documentation

**Files:**

- Modify: `packages/lint/CLAUDE.md` — "Why the root config is off-limits" is now false and must
  say the OPPOSITE: root registration is live, what made it possible (in-repo mirror), and that
  the five nested configs still own per-tree ENABLEMENT and options. Rewrite "Where this is
  registered", keep "The cost of nesting" (still true — the restated override blocks are still
  required because nested-config globs are directory-relative).
- Modify: `docs/plans/2026-08-02/1744-no-source-tests-design.md` — its "the plugin CANNOT move to
  the repo root" section gets a dated addendum pointing here; flip nothing else.
- Modify: `docs/plans/2026-08-02/2212-arch-rules-review.md` — mark recommendation A done, set
  `status: completed` if Phases 1–2 landed too.

**Step 1:** Make the edits. **Step 2:** Commit:
`git commit -m "docs(lint): record root-level plugin registration"`

### Task 11: Verify the hole actually closed

**Step 1:** Write a throwaway violation in a previously-unprotected tree, e.g.
`packages/utils/src/scratch.test.ts` containing `import { readFileSync } from "node:fs";` plus a
trivial test using it.

**Step 2:** Run `pnpm lint:tests`.
Expected: **FAIL** with a `wallow(no-source-tests)` diagnostic naming that file. This is the
one observable behaviour the whole phase exists to produce; if it passes, STOP — the
registration didn't reach that tree (check `extends` chains and `scripts/lint-tests.sh`'s file
enumeration) before touching anything else.

**Step 3:** Delete the throwaway file. Run `pnpm check` one final time. Expected: exit 0.

---

## Wrap-up

### Task 12: Beads and session completion

**Step 1:** File follow-up beads (justification path = this plan + the review doc):

- `package-dag` oxlint rule — resolution-based cross-package direction, adopt-if-needed per the
  review doc §4D trigger conditions. Priority low.
- knip exports-category adoption, IF Task 5 scoped it out.
- Nested-config registration cleanup (remove the now-redundant per-tree `jsPlugins` entries)
  after Phase 3 soaks — include the `packages/lint/CLAUDE.md` claim that a config registering
  without depending cannot load, which inverts once only the ROOT registers.

**Step 2:** Mark this plan `completed`. Close/update beads touched.

**Step 3:** Session completion per root `CLAUDE.md`:
`git pull --rebase && bd dolt push && git push`, then verify `git status` is up to date AND
`git ls-remote origin refs/dolt/data` moved.

---

## Task dependency shape

Phases 1, 2, and 3 are mutually independent (different files; only `package.json`
scripts/devDeps and `js.yml` overlap, and those edits compose). Within each phase the tasks are
strictly sequential. Task 11 must follow Task 9. If any phase stalls, the other two still land.

## Rollback

Each phase is one to four small commits touching disjoint config; `git revert` of a phase's
commits restores the prior gate. No migrations, no generated-artifact changes, no API surface.
