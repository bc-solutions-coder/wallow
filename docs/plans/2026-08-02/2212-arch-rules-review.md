# Architecture-rules review: replacing structure tests with fast tooling

**status: active**

A review of Wallow's lint-rule strategy (the `wallow/*` oxlint plugin + import bans) against how
the wider ecosystem enforces monorepo architecture, with a recommended toolchain for the pnpm
workspace. Research was done by three agents on 2026-08-02: one surveying standalone tools (facts
verified live against registry.npmjs.org), one reading the actual configs of ~10 large TS
monorepos (Nx, Sentry, Grafana, Backstage, tRPC, Next.js, Storybook, Turborepo), and one
inventorying this repo. Every external claim below carries its source.

## 1. Verdict on the current direction

**The bet is correct and independently validated.** Across every large repo surveyed, boundaries
and structure are enforced by *lint rules and repo-level check tools*, never by spec files reading
source off disk — zero instances of the "architecture unit test" pattern were found in any of
them. Two of those repos made the exact same toolchain bet as Wallow: Sentry is a pnpm workspace
formatting with oxfmt ([eslint.config.ts](https://github.com/getsentry/sentry/blob/master/eslint.config.ts)),
and Storybook lints with oxlint including its own custom monorepo-import rules
([code/.oxlintrc.json](https://github.com/storybookjs/storybook/blob/next/code/.oxlintrc.json)).

The "fitness function" literature agrees: an import-lint rule preventing `ui/` from importing
`db/` *is* the fitness function — it does not need to be a test
([fitness-functions guide](https://mikaelvesavuori.se/blog/2023-08-20_The-Up-and-Running-Guide-to-Architectural-Fitness-Function)).
The only mainstream tool arguing for a separate mechanism is Nx Conformance (paid), and its
argument is coverage of non-TS graph edges, not that tests are a better vehicle
([Nx Conformance docs](https://nx.dev/docs/enterprise/powerpack/conformance)). The ArchUnit-style
TS libraries (tsarch, ArchUnitTS, ts-arch-unit) all run **only inside test runners** — adopting
any of them would recreate the problem being escaped.

The no-source-tests sweep (delete the constraint rather than relocate it, 77 specs in one pass)
matches ecosystem practice exactly. Backstage, the best-run repo surveyed, goes further:
invariants are *computed and rewritten* (`yarn fix`), never asserted
([repo fix](https://github.com/backstage/backstage/blob/master/packages/cli-module-maintenance/src/commands/repo/fix.ts)).

## 2. The eight patterns that recur in well-run monorepos

Ranked by how commonly they appeared in the config-reading survey:

1. **Boundaries are lint rules, not tests** — every repo. Tag-based (Nx), element-type
   (Sentry via eslint-plugin-boundaries), or path-scoped `no-restricted-imports` overrides
   (Grafana, Nx's own repo, Storybook). Wallow's root config + `zone-dag` is this pattern.
2. **Invariants live in ONE root-level place, never per-package** — one lint config, one
   constraints file, one fix command. Per-package assertions are exactly what proved unscalable
   here.
3. **Generate the structure instead of asserting it** — tRPC generates every package's
   `exports`/`main`/`types` from an entrypoint list
   ([entrypoints.ts](https://github.com/trpc/trpc/blob/main/scripts/entrypoints.ts)); Backstage's
   `repo fix` derives `typesVersions` and `exports['./package.json']` from declared exports;
   Nx/turbo generators scaffold new packages correct-by-construction
   ([turbo gen](https://turborepo.dev/docs/guides/generating-code), Plop under the hood).
4. **Every check ships an autofix, and the failure message names the fix command** —
   `syncpack fix`, `manypkg fix`, Backstage printing "run 'yarn fix' to fix them".
5. **A ratchet for adopting a rule against existing code** — Grafana's
   `eslint-suppressions.json` ([ESLint bulk suppressions](https://eslint.org/blog/2025/04/introducing-bulk-suppressions/)),
   Next.js's `tsec-exemptions.json`, dependency-cruiser's `--known-violations` baseline. Freeze
   today's violations, enforce all new code, land the rule in one PR.
6. **Convention-by-construction via a declared role** — Backstage's `backstage.role` field
   determines build, test env, and lint config, so there is nothing per-package left to police
   ([build system docs](https://backstage.io/docs/tooling/cli/build-system/)). The
   highest-leverage single idea found.
7. **A few plain Node check scripts for leftovers no tool models** — but validating *generated
   artifacts and docs*, never source anatomy (Next.js
   [check-manifests.js](https://github.com/vercel/next.js/blob/canary/scripts/check-manifests.js)).
8. **AST-level custom rules when the constraint is genuinely syntactic** — Next.js's ast-grep
   YAML rules ([sgconfig.yml](https://github.com/vercel/next.js/blob/canary/sgconfig.yml)),
   Sentry/Grafana/Storybook first-party lint plugins. Wallow's `wallow/*` plugin is this.

## 3. Gap analysis — what Wallow enforces today vs. by nothing

From the repo survey (facts, `file:line` refs in the survey; re-verify before acting):

| Invariant class | Enforced today by | Gap |
| --- | --- | --- |
| Intra-app layering (app/features/shared DAG, barrel-only entry) | `wallow/zone-dag`, resolution-based | Only in the 5 plugin trees |
| Retired-API / facade bans | root `no-restricted-imports` (string globs) | Import-side only; a stray *manifest declaration* (e.g. a second package declaring `@tanstack/react-query`) is caught by nothing since the manifest sweep died |
| Bottom-of-graph purity (env/logger/utils) | stricter `no-restricted-imports` override | Same glob limitation |
| Cycles | `import/no-cycle` (native oxlint) | Fine |
| Tailwind/catalog discipline, no-source-tests | `wallow/*` jsPlugin rules | **Silent in 11 packages + fork-smoke** (auth, config, env, lint, logger, query, sdk, styles, testing, utils, examples) because the plugin registers only in 5 nested configs |
| package.json shape (exports ↔ publishConfig ↔ tsconfig.build ↔ vite entries — the four-edit trap) | **Nothing until `check:exports`**, which needs a full build first | Live gap; bit this very branch (`packages/testing` navigation-escape subpath) |
| Unused files / exports / undeclared deps | Nothing (charter/orphan sweeps deleted) | Open |
| Published-artifact shape | publint + attw (`check:exports`) | Covered; overlaps with nothing above — keep |

The root cause of the 11-package hole is `packages/sdk/src/oxlint-guardrails.test.ts`: it copies
the root config to a temp dir and runs oxlint there, and any root `jsPlugins` entry makes that
copy unloadable (specifiers resolve from the config file's own directory —
`packages/lint/CLAUDE.md`). **The last big structure-adjacent spec is the thing blocking the lint
plugin from protecting the whole repo.** It is doctrine-compliant (tool output, not source text),
but its *technique* (temp-dir copy) is what costs the coverage.

## 4. Recommendations

Ordered by leverage per unit of effort. A/B are cheap and close live gaps; C/D are additive.

### A. Unlock root-level `jsPlugins` registration (zero new tools)

Rework `oxlint-guardrails.test.ts` to stop copying the root config to a temp dir — e.g. run the
real binary against in-repo fixture files (the `packages/lint/fixtures/` pattern: lint-ignored,
oxfmt-ignored directory), so module specifiers resolve normally. Then register
`@bc-solutions-coder/lint` once at the root and delete the five restated override blocks the
nesting forced (`packages/lint/CLAUDE.md` "The cost of nesting"). This closes the 11-package hole,
removes the "future edit to a root app block silently stops applying" trap, and makes
`no-source-tests` genuinely repo-wide. Verify oxlint ≥1.76 behaviour first; the constraint was
measured on 1.74.

### B. `sherif` for package.json hygiene (one binary, sub-second, zero config)

[sherif](https://github.com/QuiiBz/sherif) (Rust, v1.13.0 2026-07, ~456k weekly downloads) checks
ten fixed manifest rules — duplicate dependency versions, types-in-dependencies, unordered deps,
root-package fields — without needing `node_modules`, with `--fix`. Add to `pnpm check` before
the build step so manifest mistakes fail in milliseconds, not after a build.

If a *configurable* policy is needed — e.g. "only `packages/query` may declare
`@tanstack/react-query`" as a banned version-group elsewhere — use
[syncpack](https://syncpack.dev/) v15 (Rust core since v14, `lint`/`fix`/`format` split) instead
of or alongside sherif. That one rule resurrects the deleted manifest sweep as config, not a test.

**The four-edit trap wants a generator, not a checker** (pattern 3): a small script that derives
`publishConfig.exports`, the `tsconfig.build.json` include, and the vite `entries` from the source
`exports` map — the tRPC/Backstage "fix, don't assert" shape. That folds into the planned
generator work; sherif/syncpack cover the interim.

### C. `knip` for the unused/undeclared class

[knip](https://knip.dev/features/monorepos-and-workspaces) (v6.31.0, ~12.2M weekly downloads)
reads `pnpm-workspace.yaml` natively and finds unused files, unused exports, unlisted and unused
dependencies — the class the deleted charter/orphan sweeps used to cover and nothing covers now.
Sentry, Grafana, and Backstage all run it. Expect a config-tuning session (`entry`/`project`
globs per workspace, `ignoreDependencies`) before it runs clean.

### D. `dependency-cruiser` only if graph rules outgrow oxlint

[dependency-cruiser](https://github.com/sverweij/dependency-cruiser/blob/main/doc/rules-reference.md)
(v18, no ESLint, ~3M weekly) is the strongest standalone graph tool: regex rules with
capture-group backreferences (sibling isolation in one rule), `dependencyTypes` (devDependency
leaking into prod code), orphans, `reachable`, and a known-violations baseline. But it can take
>1 min on big repos (mitigate with `--cache`), and `zone-dag` + `no-restricted-imports` +
`no-cycle` already cover today's stated invariants. Hold it in reserve; adopt when a rule needs
resolution-aware *cross-package* direction that globs can't express and A's root registration
doesn't reach.

### E. ast-grep as the escape hatch, not a second lint layer

[ast-grep scan](https://astgrep.com/reference/yaml.html) (YAML rules, tree-sitter, all cores,
SARIF/GitHub output) is what Next.js uses for structural rules its linter can't express. Its niche
here: repo-wide AST rules that don't depend on per-package config registration. If A lands, the
`wallow/*` plugin covers that niche natively — prefer one custom-rule system. Reach for ast-grep
only for non-JS languages or if a rule is needed in a tree oxlint doesn't lint.

### What NOT to adopt (evaluated and rejected)

- **tsarch / ArchUnitTS / ts-arch-unit** — no CLI mode; test-runner only. The pattern being retired.
- **eslint-plugin-boundaries / @nx/enforce-module-boundaries** — require ESLint (and Nx's project
  graph); a second lint toolchain for constraints oxlint + zone-dag already express.
- **madge** — stale since 2024-08, documented missed-cycle bugs
  ([issue #447](https://github.com/pahen/madge/issues/447)); `import/no-cycle` is native and already on.
- **turbo boundaries** — still experimental with an open RFC
  ([reference](https://turborepo.dev/docs/reference/boundaries)); built-ins are a subset of
  dependency-cruiser; requires adopting Turborepo.
- **Sheriff / good-fences-rs** — CLI-capable encapsulation tools, but pre-1.0 and ~1y stale;
  `zone-dag`'s barrel-only rule already covers the encapsulation idea.
- **Yarn constraints** — the best-in-class manifest engine, but Yarn-only; syncpack is the pnpm
  substitute.

### Proposed `pnpm check` shape (after A–C)

```
format:check → lint → lint:tests → sherif/syncpack lint → knip → build → typecheck → test → check:exports
```

Manifest and dead-code failures then land in seconds, before the build. (Note: root
`package.json` currently runs build *before* typecheck/test, which contradicts both the CLAUDE.md
summary and the no-source-tests plan line 288 — align the docs or the script while in here.)

## 5. Source index

- Tool facts verified against registry.npmjs.org on 2026-08-02 (versions/dates/downloads).
- dependency-cruiser: [rules ref](https://github.com/sverweij/dependency-cruiser/blob/main/doc/rules-reference.md) · [options](https://github.com/sverweij/dependency-cruiser/blob/main/doc/options-reference.md)
- oxlint: [no-restricted-imports](https://oxc.rs/docs/guide/usage/linter/rules/eslint/no-restricted-imports) · [import/no-cycle](https://oxc.rs/docs/guide/usage/linter/rules/import/no-cycle) · [nested config](https://oxc.rs/docs/guide/usage/linter/nested-config) · [JS plugins alpha](https://oxc.rs/blog/2026-03-11-oxlint-js-plugins-alpha) · [type-aware stable](https://oxc.rs/blog/2026-07-22-type-aware-linting-stable)
- knip: [monorepos](https://knip.dev/features/monorepos-and-workspaces) · sherif: [repo](https://github.com/QuiiBz/sherif) · syncpack: [v14 migration](https://syncpack.dev/guide/migrate-v14/) · manypkg: [repo](https://github.com/Thinkmill/manypkg)
- publint: [rules](https://publint.dev/rules) · attw: [CLI](https://github.com/arethetypeswrong/arethetypeswrong.github.io/blob/main/packages/cli/README.md)
- Repo configs read: [Nx eslint.config.mjs](https://github.com/nrwl/nx/blob/master/eslint.config.mjs) · [Sentry eslint.config.ts](https://github.com/getsentry/sentry/blob/master/eslint.config.ts) · [Grafana eslint.config.js](https://github.com/grafana/grafana/blob/main/eslint.config.js) + [yarn.config.cjs](https://github.com/grafana/grafana/blob/main/yarn.config.cjs) · [Backstage repo fix](https://github.com/backstage/backstage/blob/master/packages/cli-module-maintenance/src/commands/repo/fix.ts) + [peer-deps](https://github.com/backstage/backstage/blob/master/packages/repo-tools/src/commands/peer-deps/peer-deps.ts) · [tRPC entrypoints.ts](https://github.com/trpc/trpc/blob/main/scripts/entrypoints.ts) · [Next.js sgconfig.yml](https://github.com/vercel/next.js/blob/canary/sgconfig.yml) + [ast-grep rule](https://github.com/vercel/next.js/blob/canary/.config/ast-grep/rules/no-typeof-window-require.yml) · [Storybook .oxlintrc.json](https://github.com/storybookjs/storybook/blob/next/code/.oxlintrc.json)
- Nx boundaries: [docs](https://nx.dev/features/enforce-module-boundaries) · Conformance: [docs](https://nx.dev/docs/enterprise/powerpack/conformance)
- Ratchet: [ESLint bulk suppressions](https://eslint.org/blog/2025/04/introducing-bulk-suppressions/)
- Fitness functions: [guide](https://mikaelvesavuori.se/blog/2023-08-20_The-Up-and-Running-Guide-to-Architectural-Fitness-Function) · [ArchUnitTS positioning](https://lukasniessen.github.io/ArchUnitTS/)
