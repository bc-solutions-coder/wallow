**status: active**

# Spike: empirical validation of path-alias candidates (wallow-web)

Every result below was produced by running the command shown, in the worktree
`/Users/traveler/Repos/Wallow-alias-research`, on the branch named. Nothing here is
inferred from documentation.

Toolchain as measured: Node 24.11.1, pnpm 10.20.0, Vite 8.1.4, Vitest 4.1.10,
TypeScript 5.9.3, `@tanstack/start-plugin-core` 1.171.24, Nitro 3.0.260610-beta.

## Verdict up front

| Candidate | Verdict |
| --- | --- |
| **A2 — Vite 8 native `resolve.tsconfigPaths: true`** | **VIABLE, and the winner.** 8/8 checks pass, zero dependencies, zero source churn, zero workarounds. |
| A — `vite-tsconfig-paths` plugin | Viable, 8/8 pass — but strictly dominated by A2. Vite 8 prints a deprecation-style notice telling you to delete the plugin. |
| B — Node `package.json` `imports` (`#app/*`) | Viable ONLY with an ordering-sensitive 5-entry fallback array per zone, plus rewriting 64 source files. 8/8 pass once that workaround is in place. Not recommended. |
| C — shared build-config package | **Not spiked.** A and B both passed, so the fallback was not needed. |

**Nitro bundle runtime result (the disqualifying check): A, A2, and B all pass.** Every
candidate produced `.output/server/index.mjs` at exactly 18071 bytes — byte-size identical
to baseline — booted under `node .output/server/index.mjs`, served `/bff-demo` as
`HTTP 200, 7213 bytes` (identical to baseline), and logged zero errors. `grep -rE
'@(app|features|shared)/|#(app|features|shared)/' .output/` returned **0 matches** in all
three. No candidate leaks a bare alias specifier into the production server bundle.

## Baseline (untouched `research/alias-architecture`)

`pnpm install` succeeded with no auth failure — `NODE_AUTH_TOKEN` was not needed; the
lockfile was up to date and every `@bc-solutions-coder/*` dep resolved from the workspace.
The only npm noise is a pnpm warning about the project-level `.npmrc` auth line, which is
cosmetic.

One setup gotcha worth recording: `pnpm --filter @bc-solutions-coder/sdk build` alone is
**not** enough. A typecheck after only the SDK build produced 30+ `TS2307 Cannot find
module '@bc-solutions-coder/ui' / '/query' / '/styles' / '/testing'` errors. A full
`pnpm build` (all 11 workspace projects, 7.7s) is required before any app typechecks.

| Check | Command | Result |
| --- | --- | --- |
| typecheck | `pnpm --filter @bc-solutions-coder/wallow-web typecheck` | PASS, 2.7s |
| test (both projects) | `pnpm --filter @bc-solutions-coder/wallow-web test` | PASS — **85 files, 630 tests**, 11.7s |
| build | `pnpm --filter @bc-solutions-coder/wallow-web build` | PASS, 1.7s; `.output/server/index.mjs` = 18071 bytes |
| bundle grep | `grep -rE '@(app\|features\|shared)/' .output/` | 0 matches |
| nitro boot | `node .output/server/index.mjs` + curl | `/bff-demo` → **HTTP 200, 7213 bytes** |
| vite dev | `vite dev` + curl | ready in 346ms; `/bff-demo` → HTTP 200, 5663 bytes |
| route codegen | `git status src/app/routeTree.gen.ts` after build | unchanged (regenerated identical) |
| importProtection | see probe below | ENFORCED — build exits 1 |

Baseline is fully green. Nothing red was inherited.

### Pre-existing finding: `/` returns HTTP 500 in every configuration

Curling `/` on the booted Nitro bundle returns **HTTP 500** on the untouched baseline:

```
Error: Invalid BFF environment configuration:
  - Missing required environment variable: OIDC_ISSUER
  - Missing required environment variable: OIDC_CLIENT_ID
  ... COOKIE_PASSWORD
    at loadBffConfigFromEnv (.output/server/_ssr/bff-*.mjs:1839:45)
```

This is missing backend/OIDC env, not an alias problem. `/bff-demo` is the correct
backend-free probe route (it is also the only route `apps/wallow-web/e2e/routes.spec.ts`
qualifies as backend-free). All candidate comparisons use `/bff-demo`.

### Correcting the premise of check 8

The task described check 8 as "try importing `redis` (or `@app/lib/bff`) from a client-side
module and confirm the build still rejects it." **That probe does not fire, on the untouched
baseline.** I ran both on `spike/control`:

- `import { createClient } from "redis"` in `src/shared/components/ready-indicator.tsx` →
  `BUILD_EXIT=0`. Only cosmetic "Module "net"/"tls" has been externalized for browser
  compatibility" warnings.
- `import { handleApiRequest } from "@app/lib/bff"` in the same file → `BUILD_EXIT=0`, and
  `vite dev` served `/bff-demo` HTTP 200 with no protection error.

In both cases redis **actually shipped in the client bundle**: `.output/public/assets/index-*.js`
contains `@redis/client` package metadata and `RESP2` transform code.

Reading `@tanstack/start-plugin-core/dist/esm/import-protection/defaults.js` explains why —
the default client rules are only:

```js
client: { specifiers: ["@tanstack/{react,solid,vue}-start/server"], files: ["**/*.server.*"] }
```

Neither `redis` nor `@app/lib/bff` matches. So I built a probe that genuinely exercises the
guard *through an alias*, which is the alias-relevant question:

**Probe** (committed at `spike-tools/import-protection-probe.sh` on `spike/control`): create
`src/shared/lib/probe.server.ts`, import it from the client component
`src/shared/components/ready-indicator.tsx` via the zone alias, build, assert non-zero exit.

Baseline result — `BUILD_EXIT=1`:

```
  Import: "src/shared/lib/probe.server"
  Resolved: src/shared/lib/probe.server.ts
    4. src/shared/components/ready-indicator.tsx (import "src/shared/lib/probe.server")
```

That is the control every candidate is measured against below.

## Results matrix

Checks: 1 typecheck · 2 vitest node · 3 vitest browser · 4 build + Nitro bundle emitted ·
5 **bundle grep + actual runtime boot** · 6 `vite dev` · 7 route codegen · 8 importProtection
through the alias.

| # | Check | Baseline | A (plugin) | A2 (native) | B (`imports`) |
| --- | --- | --- | --- | --- | --- |
| 1 | typecheck | PASS | PASS 3.2s | **PASS 3.1s** | PASS — **only after workaround**, see below |
| 2 | vitest node | PASS 23f/209t | PASS 23f/209t | **PASS 23f/209t** | PASS 23f/209t (after workaround + 1 spec edit) |
| 3 | vitest browser (real Chromium) | PASS 61f/416t | PASS 61f/416t | **PASS 61f/416t** | PASS 61f/416t (after workaround) |
| 4 | build + `.output/server/index.mjs` | PASS 18071B | PASS 18071B | **PASS 18071B, 1.8s** | PASS 18071B, 1.8s |
| 5 | grep bundle → **boot + curl** | 0 hits; 200/7213 | 0 hits; **200/7213** | 0 hits; **200/7213** | 0 hits; **200/7213** |
| 6 | `vite dev` + curl | 200/5663 | 200/5663 | **200/5663, ready 364ms** | 200/5663, ready 805ms |
| 7 | `routeTree.gen.ts` regenerates | clean | clean | **clean** | clean |
| 8 | importProtection via alias | exit 1 | exit 1 | **exit 1** | exit 1 |
| — | `pnpm lint` / `format:check` | PASS | not run | **PASS** | **PASS** |

Full-suite totals: baseline 85 files/630 tests; every candidate 84 files/625 tests. The delta
is exactly the deleted `src/alias-map.test.ts` and its 5 assertions — the spec that existed
solely to pin the hand-mirrored duplication, which every candidate removes.

## Spike A — `vite-tsconfig-paths` (branch `spike/a-vite-tsconfig-paths`)

Added `vite-tsconfig-paths@6.1.1`, deleted `aliases.ts` and `src/alias-map.test.ts`, dropped
the `resolve.alias` zone entries from `vite.config.ts` and the `resolve.alias` from both
vitest projects, kept `tsconfig.json` `paths` as the only declaration.

All 8 checks pass. Workarounds required: **none** beyond adding `plugins: [tsconfigPaths()]`
to `vite.config.ts` *and separately to each of the two vitest projects* — the plugin does not
inherit into `test.projects[]`, so it is named three times.

**But the plugin prints this on every dev, build and test run:**

```
The plugin "vite-tsconfig-paths" is detected. Vite now supports tsconfig paths resolution
natively via the resolve.tsconfigPaths option. You can remove the plugin and set
resolve.tsconfigPaths: true in your Vite config instead.
```

That message is the single most useful thing this spike produced, and it motivated A2.

## Spike A2 — Vite 8 native `resolve.tsconfigPaths` (branch `spike/a2-native-vite-tsconfigpaths`) — RECOMMENDED

Same as A with the dependency **removed** (`pnpm remove vite-tsconfig-paths`) and replaced by
one line in `vite.config.ts`:

```ts
resolve: {
  tsconfigPaths: true,
  dedupe: ["react", "react-dom"],
}
```

and in `vitest.config.ts`, per project:

```ts
{ ...node, resolve: { tsconfigPaths: true } },
{ ...browser, resolve: { tsconfigPaths: true, alias: { "node:async_hooks": nodeAsyncHooksShim } } },
```

`tsconfig.json` `paths` is now the single, only declaration of the three zones.

**All 8 checks pass. Zero workarounds. Zero new dependencies. Zero source-file churn.**

Total diff vs base — `git diff --stat research/alias-architecture spike/a2-native-vite-tsconfigpaths`:

```
 apps/wallow-web/aliases.ts            | 34 -----------------
 apps/wallow-web/src/alias-map.test.ts | 70 -----------------------------------
 apps/wallow-web/tsconfig.json         |  7 ++--
 apps/wallow-web/vite.config.ts        | 10 ++---
 apps/wallow-web/vitest.config.ts      |  6 +--
 5 files changed, 9 insertions(+), 118 deletions(-)
```

It deletes 118 lines and adds 9. The four coupled declaration sites collapse to one.

A2's importProtection error is also strictly better than baseline's — it reports the alias as
written plus a line/column, where baseline reported only the rewritten relative path:

```
  Import: "@shared/lib/probe.server"
  Resolved: src/shared/lib/probe.server.ts
    4. src/shared/components/ready-indicator.tsx:12:8 (import "@shared/lib/probe.server")
```

## Spike B — Node `package.json` `imports` (branch `spike/b-node-subpath-imports`)

Added an `imports` field, removed tsconfig `paths` and all `resolve.alias` zone entries,
deleted `aliases.ts` and `alias-map.test.ts`, and rewrote **all** 87 zone specifiers across
62 source files from `@zone/` to `#zone/` (one `sed`; converting all rather than a sample,
because the two mechanisms cannot coexist half-way).

Result: **no plugin needed and no `resolve` config at all** — Vite and Vitest both read
`imports` natively. But getting TypeScript and Vite to agree on one shape took four attempts.

### The workaround, in full — this is the finding

The obvious mapping `"#shared/*": "./src/shared/*"` **fails TypeScript hard**:

```
src/shared/components/PublicLayout.tsx(5,56): error TS2307: Cannot find module
  '#shared/lib/site-links' or its corresponding type declarations.
```

**85 of 87 specifiers unresolved.** TS 5.9.3 under `moduleResolution: "Bundler"` reads the
`imports` field but does **not** do extension inference on the substituted target — it wants
the wildcard substitution to name a real file. Vite does the opposite: it infers extensions
but takes only the first array entry.

Measured shapes:

| `imports` target shape | `tsc` TS2307 count | Vite (browser project) |
| --- | --- | --- |
| `"./src/<zone>/*"` (bare) | **85 errors** | PASS 61f/416t |
| `"./src/<zone>/*.ts"` | 24 errors | not run |
| `{ "types": "…*.ts", "default": "…*" }` | 24 errors | not run |
| `[".ts", ".tsx", bare]` (extension-first array) | **0 errors** | **FAIL — 37 files, 58 tests** |
| `[bare, ".ts", ".tsx", "*/index.ts", "*/index.tsx"]` | **0 errors** | **PASS 61f/416t** |

The extension-first array fails Vite like this:

```
[vite] Internal server error: Failed to resolve import "#shared/components/SelectControl"
from "src/features/organizations/components/OrganizationDetail.tsx". Does the file exist?
```

— every `.tsx` module and every barrel directory, because Vite took entry 1 (`*.ts`) and did
not walk the array.

Only the **bare-first 5-entry array** satisfies both, and it works for a subtle reason:
TypeScript walks the whole array until something resolves, while Vite takes the first entry
and applies its own extension inference to it. The order is load-bearing. The final field is
15 lines of JSON where the current `aliasDirs` is 3:

```json
"imports": {
  "#shared/*": [
    "./src/shared/*",
    "./src/shared/*.ts",
    "./src/shared/*.tsx",
    "./src/shared/*/index.ts",
    "./src/shared/*/index.tsx"
  ],
  … same 5 entries for #app/* and #features/*
}
```

That is an ordering-sensitive, undocumented interaction between two resolvers — precisely the
kind of accreted complexity this project is trying to eliminate. It is also a landmine: a
future contributor "tidying" the array into alphabetical order silently breaks the browser
suite for `.tsx` files only.

### Second workaround: a policy spec hardcodes the `@` spelling

`src/shared/lib/request-origin.test.ts:133` asserts on source text:

```js
expect(source).toMatch(/from\s+"@shared\/lib\/request-origin"/u);
```

The `sed` misses it (the regex is escaped), so the node project failed with
`expected 'import { createWallowSdk…' to match /from\s+"@shared\/lib\/request-ori…/`
until the literal was updated by hand. `src/zone-dag.test.ts` needs the same treatment
(`specifier.startsWith("@app/")` → `"#app/"`). Any rename of the prefix must sweep these.

### B's other measured answers

- **TS 5.9.3 + `moduleResolution: "Bundler"`:** resolves `#app/*` for typecheck **only** with
  the bare-first array. Go-to-definition follows the same resolver, so it works under that
  shape and is broken under the naive one.
- **Vitest browser mode:** resolves `#` specifiers natively, no config. PASS 61f/416t.
- **oxlint / oxfmt:** both handle `#` specifiers cleanly. `pnpm lint` passes; `pnpm format:check`
  passes after `pnpm format`. oxfmt sorts `#shared/...` into its own group after external
  packages — the same slot the `@shared` form occupied, so import ordering is unchanged.

Source churn: 64 files changed, 88 insertions, 158 deletions — versus **zero** source files
touched by A2.

## Scaling: adding a fourth zone `@entities/*` / `#entities/*`

Actually performed on `spike/a2-fourth-zone` and `spike/b-fourth-zone`: created
`src/entities/probe-entity.ts`, declared the zone, imported it from a real client component,
then ran typecheck + browser project + build.

| Candidate | Files to declare a fourth zone | Verified |
| --- | --- | --- |
| Baseline (today) | **3** — `aliases.ts`, `tsconfig.json`, and the `["@app","@features","@shared"]` assertion in `alias-map.test.ts` | by inspection |
| **A2** | **1** — `tsconfig.json` | typecheck 0 errors, browser 61f/416t, `BUILD_EXIT=0` |
| B | **1** — `package.json` (but 5 array lines, not 1) | typecheck 0 errors, node 23f/209t, `BUILD_EXIT=0` |

Caveat that applies equally to both: `src/zone-dag.test.ts:229` asserts the exact zone set
`["app", "features", "root", "shared"]`. It did **not** fail when the fourth zone was added
(an unrecognised `#entities/` specifier falls through its classifier), so it does not block
the change — but it must be updated for the new zone to actually be governed by the import
DAG. That is one extra file for either candidate, and it is policy, not plumbing.

## Recommendation

**A2 — `resolve.tsconfigPaths: true`.** It is the only candidate that reaches the goal with
no new dependency, no plugin, no source churn, and no workaround. It collapses four coupled
declaration sites to one (`tsconfig.json` `paths`), deletes `aliases.ts` and the
`alias-map.test.ts` spec that existed only to police the duplication, and empirically
preserves every behaviour that matters: real-Chromium browser tests, the Nitro production
bundle's runtime, TanStack Start route codegen, and importProtection's server-only boundary.

The whole change is 9 added lines against 118 deleted.

B works, but only by discovering an ordering-sensitive fallback array that no documentation
describes, and it costs 64 rewritten source files to get there. C was not needed.

Not covered by this spike, and worth a follow-up: `apps/wallow-auth` was not converted (the
spike was scoped to wallow-web, the harder app), and no Playwright E2E suite was run — the
Nitro bundle was booted and curled directly instead.

## Branches left in place

| Branch | Contents |
| --- | --- |
| `spike/control` | importProtection probe harness (`spike-tools/import-protection-probe.sh`) |
| `spike/a-vite-tsconfig-paths` | Spike A |
| `spike/a2-native-vite-tsconfigpaths` | **Spike A2 — the recommendation** |
| `spike/a2-fourth-zone` | A2 + `@entities/*` |
| `spike/b-node-subpath-imports` | Spike B |
| `spike/b-fourth-zone` | B + `#entities/*` |

Nothing was pushed. `research/alias-architecture` is clean.
