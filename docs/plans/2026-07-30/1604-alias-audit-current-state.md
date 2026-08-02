**status: active**

# Alias & module-resolution audit — current state (read-only)

Worktree: `/Users/traveler/Repos/Wallow-alias-research`, branch `research/alias-architecture`,
at `1585962c test(ui): point the CSS-entry guard at the zoned styles.css`.

This document describes **what exists and why**. It proposes nothing.

---

## 0. Corrections to the briefing

Two of the premises handed to me are wrong; recording them first so nothing downstream is
built on them.

1. **`apps/wallow-web/src/routes/` does not exist.** The only `routes` directories under
   `apps/` are `apps/wallow-web/src/app/routes`, `apps/wallow-auth/src/app/routes`, and
   `apps/examples/minimal-app/src/routes` (the last is the deliberately-flat example app).
   There is no dead duplicate to investigate.
2. **The alias machinery did *not* accrete during BFF/auth hardening.** It is 14 commits old,
   all landed 2026-07-30, as one deliberate restructure. Detail in §3.6. The *other*
   workarounds in the same files (the `use-sync-external-store` shim, `node:async_hooks`,
   `ssr.noExternal`, `copyPublicDir`) *do* have the accretion history the hypothesis predicts —
   just not the aliases.

Verified drift worth recording: `tsconfig.base.json:2-6` claims "TypeScript 7 (Project Corsa)"
semantics, every one of the 11 workspace manifests pins `"typescript": "^5.6.0"`, and
`pnpm-lock.yaml:3512` resolves that to **5.9.3**. (`typescript@5.6.1-rc` at `pnpm-lock.yaml:3507`
is a transitive dep of `@arethetypeswrong/cli`, not a workspace member's.) Other installed
versions confirmed from the lockfile: Vite `8.1.4` (`:3660`), Vitest `4.1.10` (`:3725`), Nitro
`3.0.260610-beta` (`:2995`), TanStack Start `1.168.32` (`pnpm-workspace.yaml:10`).

---

## 1. Inventory of every module-resolution declaration site

### 1a. The zone-alias triangle (the subject of this audit)

| # | Declaration site | What it maps | Consumed by | Duplicated where |
|---|---|---|---|---|
| 1 | `apps/wallow-web/aliases.ts:17-21` (`aliasDirs`) | `@app`→`src/app`, `@features`→`src/features`, `@shared`→`src/shared` | its own `resolveAlias`, `src/alias-map.test.ts:7` | **byte-identical** to `apps/wallow-auth/aliases.ts` (verified `diff` clean) |
| 2 | `apps/wallow-web/aliases.ts:29-34` (`resolveAlias`) | same three, keyed **with trailing slash**, values absolute + trailing slash | `vite.config.ts:56`, `vitest.config.ts:96,99` | ditto |
| 3 | `apps/wallow-web/tsconfig.json:13-17` (`compilerOptions.paths`) | `@app/*`→`./src/app/*`, `@features/*`, `@shared/*` | `tsc --noEmit`, editors | hand-mirror of #1; `apps/wallow-auth/tsconfig.json:14-18` identical |
| 4 | `apps/wallow-web/vite.config.ts:33-57` (`resolve.alias`, array form) | shim regexes + spread of #2 | `vite dev`, `vite build`, nitro output | `apps/wallow-auth/vite.config.ts:49-72` identical modulo one comment |
| 5 | `apps/wallow-web/vitest.config.ts:96,99` (per-project `resolve.alias`) | #2, plus `node:async_hooks` on browser only | `vitest run` | `apps/wallow-auth/vitest.config.ts:57-58` (no async-hooks entry) |
| 6 | `apps/wallow-web/src/alias-map.test.ts` | *asserts* #1 ≡ #3 and that #4/#5 import #1 | `pnpm test` | identical file in wallow-auth (`diff` clean) |

Net: **one source of data (`aliasDirs`), three consumers, one hand-written mirror, one test to
pin the mirror — replicated verbatim across two apps.**

### 1b. Non-alias `resolve.*` / bundler-resolution keys

| Site | Key | Value | Why (see §3) |
|---|---|---|---|
| `apps/wallow-web/vite.config.ts:51-52` | `resolve.alias` | `/^use-sync-external-store\/shim$/u` → `react`; same for `…/shim/index.js` | §3.1 |
| `apps/wallow-auth/vite.config.ts:66-67` | same | same | §3.1 |
| `apps/wallow-web/vite.config.ts:59` | `resolve.dedupe` | `["react","react-dom"]` | one React in the graph |
| `apps/wallow-auth/vite.config.ts:74` | same | same | " |
| `apps/wallow-web/vitest.config.ts:91-93` | `ssr.noExternal` | `@bc-solutions-coder/query`, `/auth` | §3.3 |
| `packages/testing/vitest.config.ts:53-55` | `ssr.noExternal` | `@bc-solutions-coder/query` | §3.3 |
| `apps/wallow-web/vitest.config.ts:99` | `resolve.alias["node:async_hooks"]` | `src/shared/testing/node-async-hooks-browser-shim.ts` | §3.2 |
| `apps/wallow-web/vite.config.ts:68`, `apps/wallow-auth/vite.config.ts:83`, `apps/examples/minimal-app/vite.config.ts` | `environments.client.build.copyPublicDir` | `true` | §3.8 |

wallow-auth carries **no** `ssr.noExternal` — asymmetric with wallow-web and with
`packages/testing`, and nothing enforces the asymmetry either way.

### 1c. `optimizeDeps.include` (browser pre-bundling) — the largest duplicated surface

| Site | Entries | Notes |
|---|---|---|
| `packages/testing/src/browser-optimize-deps.ts:11-16` | baseline: `vitest-browser-react`, both jsx runtimes, `react-dom/client` | merged by `mergeOptimizeDeps()` (`:23-31`) |
| `apps/wallow-web/vitest.config.ts:37-71` | 17 extras incl. 5 `@base-ui/react/*` subpaths | §3.7 |
| `apps/wallow-auth/vitest.config.ts:42-46` | 3 extras | |
| `packages/testing/vitest.config.ts:38-43` | 4 extras | dogfoods its own preset |
| `packages/ui/vitest.config.ts:31-70` | **37** `@base-ui/react/*` subpaths + `class-variance-authority`, `tailwind-merge` | listed **twice** — `:83` for the browser project and `:104` again for the `storybook` project, because Storybook runs its own Vite server with its own dep cache |
| `packages/forms/vitest.config.ts:28-61` | Base UI subpaths of every ui field it wraps + form runtime | comment at `:25` says every new catalog field must append here |

`packages/ui/vitest.config.ts:28-30` states the growth rule explicitly: *"EVERY component task in
the Base UI rebuild must append its own subpath here as it lands."*

### 1d. TypeScript configs

| File | `extends` | `paths` | `baseUrl` | `moduleResolution` | `include` |
|---|---|---|---|---|---|
| `tsconfig.base.json` | — | — | — | `Bundler` (`:16`) | — |
| `apps/wallow-web/tsconfig.json` | base | 3 zone entries | **none** (comment `:12` — Bundler resolves relative to the file) | inherited | `src/**/*.{ts,tsx}`, `aliases.ts`, `vite.config.ts`, `vitest.config.ts` (`:19`) |
| `apps/wallow-auth/tsconfig.json` | base | same 3 | none | inherited | same (`:20`) |
| `apps/examples/minimal-app/tsconfig.json` | base | **none** | none | inherited | `src/**`, configs |
| `packages/{auth,forms,query,styles,testing,ui}/tsconfig.json` | base | none | none | inherited | `src`, configs |
| **`packages/sdk/tsconfig.json`** | **does not extend base** — fully standalone, `target: ES2022`, no `isolatedModules`/`verbatimModuleSyntax`/`forceConsistentCasingInFileNames` | none | none | `Bundler` (own copy) | `src`, `scripts` |
| `scripts/fork-smoke/tsconfig.json` | base (outside the pnpm workspace) | none | none | inherited | |

No TypeScript **project references** anywhere. Every package typechecks independently via
`tsc --noEmit` (apps) or `tsc -p tsconfig.build.json` (packages).

### 1e. `package.json` resolution fields

- **No `"imports"` field anywhere in the workspace** — the Node subpath-imports mechanism
  (`#app/*`) is entirely unused. Verified by `rg '"imports"' --glob package.json`.
- `"exports"` is used only by publishable/linked packages: `packages/testing/package.json:10-27`
  (4 entries), and equivalently in `sdk`, `ui`, `forms`, `query`, `auth`, `styles`.
- Apps declare no `main`/`module`/`types`/`exports` — they are private and never imported.
- Apps consume packages exclusively through `workspace:*` deps
  (`apps/wallow-web/package.json` — 7 `@bc-solutions-coder/*` entries).

### 1f. `packages/testing` preset — does it inject aliases?

**No.** `createVitestProjects()` (`packages/testing/src/vitest-projects.ts:89-136`) sets only
`test.name/environment/include/exclude/testTimeout` and `optimizeDeps.include` /
`test.browser`. It exposes `nodeProjectOverrides` (`:33`) which is merged via Vite's
`mergeConfig` — but **only into the node project** (`:135`). Both apps therefore bypass it and
splice `resolve.alias` onto the returned objects by hand
(`apps/wallow-web/vitest.config.ts:96-100`, `apps/wallow-auth/vitest.config.ts:56-59`), because
the browser project has no override channel at all.

The preset's own header (`:16-19`) still documents `nodeProjectOverrides` as carrying
"wallow-web's `resolve.alias['openid-client']` + `test.server.deps.inline`" — **both of which
are gone** (`apps/wallow-web/vitest.config.ts:23-27` explains their removal). The preset's one
extension point is currently used by nobody.

### 1g. `.oxlintrc.json`

Root `.oxlintrc.json` has `no-restricted-imports` (`:30-96`) with 4 `paths` and 2 `patterns`
entries. **None of them reference the zone aliases.** They ban deleted SDK symbols, direct
`@tanstack/react-query`, SDK `dist`/`src`/`generated` deep paths, and `**/lib/wallow-sdk`-style
per-app facades. The whole block is **re-declared verbatim** (minus the react-query entry) in
the `packages/query` override at `:139-211` because oxlint has no per-name partial disable —
~70 duplicated lines.

Nested configs: `packages/ui/.oxlintrc.json`, `packages/forms/.oxlintrc.json`,
`scripts/fork-smoke/.oxlintrc.json`. Per-app overrides at `:112-131` are filename-case and
magic-number rules only, and are **enumerated app by app** (three near-identical blocks).

`zone-dag.test.ts:19-22` explains why the DAG is a spec and not an oxlint rule:
`no-restricted-imports` globs the specifier *string*, but the rule is about where a path
*resolves* — `../../shared/*` and `../../../wallow-auth/src/shared/*` are the same violation and
no single glob catches both without banning legal intra-zone imports.

### 1h. Dockerfiles / Playwright / deployment

- `apps/wallow-web/Dockerfile:25` and `apps/wallow-auth/Dockerfile:25` copy `tsconfig.base.json`
  into the image (the app tsconfig extends it), then 7 manifest COPYs before
  `RUN pnpm install --frozen-lockfile` (`:35`) and 7 source COPYs before the build RUN
  (`:68` / `:77`). `aliases.ts` rides along inside `COPY apps/wallow-web apps/wallow-web`
  (`:59`) — it is **not** independently named, so an alias file moved outside the app dir would
  silently break the image build.
- `apps/*/playwright.config.ts` and `apps/wallow-web/playwright.cross-app.config.ts` declare
  **no** resolution config at all (verified by grep) — Playwright specs live in `e2e/`, are
  compiled by Playwright's own esbuild pass, and import nothing from `src/`.
- `.github/workflows/route-tree-drift.yml:21-28,35-43` hard-codes the zoned paths
  (`apps/wallow-*/src/app/routes/**`, `apps/wallow-*/src/app/routeTree.gen.ts`) alongside the
  flat minimal-app paths. Path-filter only — it never resolves modules.
- Nitro config is inline in each app's vite config (`nitro()` / `nitro({ baseURL: VITE_BASE })`);
  there is no `nitro.config.ts`.

### 1i. `pnpm-workspace.yaml`

`packages: apps/*, apps/examples/*, packages/*` (`:1-4`). Two catalogs: `start` (3 exact pins,
`:9-12`) and `react` (5 ranges, `:14-19`). One `overrides` entry pinning `js-yaml: ^4.3.0`
(`:24-25`). Catalogs govern *versions*, never *paths* — orthogonal to aliases, but the exact-pin
rationale at `:7-8` ("two copies at runtime is a broken app") is the same singleton concern that
drives `dedupe`, `optimizeDeps.include`, and `ssr.noExternal`.

---

## 2. The coupling metric

### 2a. Files to edit to add ONE new alias (say `@entities/*` → `src/entities`) to ONE app

**Four files, in this order.** Two of them are *tests that would otherwise fail*.

1. `apps/<app>/aliases.ts` — add `"@entities": "src/entities"` to `aliasDirs` (`:17-21`).
   `resolveAlias` derives automatically.
2. `apps/<app>/tsconfig.json` — add `"@entities/*": ["./src/entities/*"]` to `paths`
   (`:13-17`). Mirror, no way to derive.
3. `apps/<app>/src/alias-map.test.ts:44` — `expect(Object.keys(aliasDirs).toSorted())
   .toEqual(["@app","@features","@shared"])`. **Hard-fails** on the new key.
4. `apps/<app>/src/zone-dag.test.ts` — three separate edits:
   - `:229` `expect([...zones].toSorted()).toEqual(["app","features","root","shared"])`
     **hard-fails** as soon as `src/entities/` contains one file, because `zoneOf()` (`:148-159`)
     derives zone names from file paths.
   - `targetOf()` (`:162-195`) only recognises `@app/`, `@shared/`, `@features/`. `@entities/foo`
     falls through to `!specifier.startsWith(".")` → `{ kind: "package" }`, so the new zone is
     **silently unpoliced** — this is a *soft* failure, worse than the hard one.
   - the DAG rules at `:237-309` would need an edge policy for the new zone.

`vite.config.ts` and `vitest.config.ts` need **no** edit — they spread `resolveAlias`. That part
of the design works.

**Both apps: ×2 = 8 file edits.** Plus prose: `docs/development/frontend-setup.md:107-127`
(the zone table and the alias-map paragraph) and `apps/CLAUDE.md:29-37`. No prose-pin test
covers those two, so they drift silently.

### 2b. Files to touch to add a NEW zoned app

Enumerated from what wallow-auth actually needed (commits `eedddfef` → `0e3b819f`):

1. `apps/<new>/package.json` (7 `workspace:*` deps)
2. `apps/<new>/aliases.ts` — **copied verbatim**
3. `apps/<new>/tsconfig.json` — `paths` block copied
4. `apps/<new>/vite.config.ts` — shim regexes + spread + `srcDirectory` + `importProtection`
   + `routeFileIgnorePattern` + `copyPublicDir`, all copied
5. `apps/<new>/vitest.config.ts` — preset call + hand-spliced `resolve.alias` on both projects
6. `apps/<new>/src/alias-map.test.ts` — copied verbatim (70 lines)
7. `apps/<new>/src/zone-dag.test.ts` — copied verbatim (329 lines)
8. `apps/<new>/src/feature-barrels.test.ts` + `feature-barrels.browser.test.tsx`
9. `apps/<new>/src/docker-workspace-copies.test.ts` + `apps/<new>/Dockerfile` (14 COPY lines)
10. `apps/<new>/playwright.config.ts`
11. `.oxlintrc.json` — a fourth near-identical `overrides` block (`:112-131`)
12. `.github/workflows/route-tree-drift.yml` — 2 path-filter blocks
13. `packages/ui/src/core/package-scaffold.test.ts:184-185` — hard-codes
    `apps/wallow-auth` and `apps/wallow-web` by name for the CSS-entry guard
14. `docs/development/frontend-setup.md`, `apps/CLAUDE.md`, root `CLAUDE.md` table

**~14 sites, of which 6 are verbatim copies of an existing app's file.**

---

## 3. Archaeology — why each non-obvious thing exists

Git history is heavily squashed; `cc8311e3 feat!: migrate apps to tanstack start and streamline
sdk` is a mega-commit that introduces several of these at once.

### 3.1 `use-sync-external-store/shim` anchored-regex aliases
`apps/wallow-web/vite.config.ts:34-52`, `apps/wallow-auth/vite.config.ts:50-67`.

**Original problem** (from the comment, which is unusually complete): the shim is CJS; rolldown
leaves its `require("react")` as a runtime `__require`, so the **built server** loads a second
React from `node_modules` alongside the bundled one. Every component reading an external store
(Base UI's `useIsHydrating`, zustand) throws "Invalid hook call" during SSR and the page falls
back to client-only rendering with an empty document.

**Why anchored regexes and not strings**: a string alias matches by **prefix**, so
`use-sync-external-store/shim` would also swallow `…/shim/with-selector` and rewrite it to the
nonexistent `react/with-selector`. That subpath has its own implementation (React ships no
`useSyncExternalStoreWithSelector`).

**History** — `git log -S 'use-sync-external-store'`: `08d402c8` (tanstack-start + BFF spike) →
`0682d684 feat(web): add zustand ui store for the dashboard nav` → `79dbb80a feat(ui): rebuild
@bc-solutions-coder/ui as a Base UI + CVA catalog` → `cc8311e3`. **This one does track the
hypothesis partially**: it first appears in the BFF spike and is re-touched each time a new
store-reading dependency (zustand, then Base UI) enters the graph. It is not auth-security
work, it is React-19-singleton work.

### 3.2 `node:async_hooks` browser shim
`apps/wallow-web/vitest.config.ts:73-82`; implementation
`apps/wallow-web/src/shared/testing/node-async-hooks-browser-shim.ts`.

`@tanstack/react-start` → `@tanstack/start-storage-context` runs `new AsyncLocalStorage()` at
module scope. A real client build never sees it because the Start plugin compiles
`createIsomorphicFn().client(…).server(…)` to the client branch — **but the vitest browser
project does not run the Start plugin**, so it loads the server branch and dies at import
("AsyncLocalStorage is not a constructor"; vitest externalises `node:async_hooks` to a throwing
proxy), taking down every spec importing `src/app/router.tsx`.

The shim (`:24-54`) implements real browser semantics rather than faking a context: no request
scope in a browser, so `getStore()` answers `undefined` and `getRouter()` falls back to the
same-origin browser SDK — exactly what the compiled client build does.

**History**: `git log -S 'node:async_hooks'` → `423e83de fix(web): forward SSR request origin
and session cookie to BFF fetches`, `a1f64f60 feat(sdk): absorb csrf, ssr context, and facade
helpers`, `cc8311e3`. **This one confirms the hypothesis** — it originates in SSR request-context
plumbing for the BFF. wallow-auth needs no such shim (its router does not pull the storage
context into browser specs), which is why the two vitest configs diverge here.

### 3.3 `ssr.noExternal` for `@bc-solutions-coder/query` / `auth`
`apps/wallow-web/vitest.config.ts:91-93`, `packages/testing/vitest.config.ts:53-55`.

Introduced in exactly one commit, `521fd1bc feat!: add packages/query and packages/auth, delete
packages/web-shell` (2026-07-29). Cause: a **linked** workspace package is neither pre-bundled
nor inlined by Vite. For the node/SSR project Vite externalises it to a bare Node import instead
of transforming its source, so SSR-side route specs never see it. This is the SSR half of the
same singleton problem `optimizeDeps.include` solves for the browser half — the commit adds both
in one hunk.

Not auth-security accretion: it is workspace-linking mechanics that arrived with the facade
packages.

### 3.4 `srcDirectory: "src/app"` + the mandatory `importProtection: { include: ["src/**"] }`
`apps/wallow-web/vite.config.ts:71-91`, `apps/wallow-auth/vite.config.ts:86-102`.

Landed as `607b9914 build(web): point the start plugin at the src/app source root` and
`9e7a1392 build(auth): …` — part of the zone restructure, three days old.

- `srcDirectory` is the **one** knob that relocates routes, router, entries and the generated
  tree together; the plugin resolves `router.routesDirectory`, `router.generatedRouteTree` and
  every entry relative to it (comment cites `start-plugin-core schema.js:48-49`,
  `planning.js:54-95`). Setting `routesDirectory: "src/app/routes"` instead would resolve to
  `src/src/app/routes`, and the `required: true` router entry would still not be found →
  hard build failure.
- `importProtection.include` is **mandatory, not optional**: with no `include`, the
  import-protection plugin uses `srcDirectory` itself as the importer scope
  (`import-protection/adapterUtils.js:23`). Narrowing `srcDirectory` to `src/app` would
  therefore silently stop enforcing the server-only/client-bundle boundary for everything under
  `src/features/**` and `src/shared/**` — i.e. it would let `app/lib/bff.ts`'s `redis`/
  `openid-client` imports reach a client bundle. This is the single most load-bearing pairing
  in the whole config, and it is why `brand-assets.test.ts:87-96` asserts the config *text*.

### 3.5 `routeFileIgnorePattern`
`apps/wallow-web/vite.config.ts:95`, `apps/wallow-auth/vite.config.ts:110`,
`apps/examples/minimal-app/vite.config.ts`.

`git log -S 'routeFileIgnorePattern'` → `be03bd56 fix(web-shell): silence route-file warnings and
HMR port collisions`, then `cc8311e3`. Cause: specs are co-located with the code they cover, so
a `*.test.tsx` under `routes/` would be codegen'd in as a route. Pure co-location consequence,
predates the zone work.

### 3.6 The trailing-slash keying of `resolveAlias`, and the existence of `aliases.ts` at all

`aliases.ts:23-28`: keyed **with** the trailing slash because Vite's object-form alias matches by
**prefix** — a bare `@app` key would also swallow a future `@application`. `@app/` cannot. The
same reasoning as the shim regexes at §3.1, one file over.

`aliases.ts:14-15`: `@app` maps to `src/app`, **not** `src` — an `@app/* → src/*` entry would
overlap the other two and give two spellings for the same module.

`aliases.ts:11-12` states the "why a .ts module" decision outright: *"Deliberately NOT a shared
build-config package: a preset would mean deep-rooted build files coupling every app to a
package, for three lines of data."* It is a `.ts` module rather than reading `tsconfig.json`
because vite/vitest need **absolute, trailing-slashed** paths and tsconfig holds relative,
glob-suffixed ones; nothing in the repo reads tsconfig at build time.

**Chronology (all 2026-07-30, in order):**

```
2434359d feat(web):  add the zone alias map and its tsconfig pin test   (3 files: aliases.ts, alias-map.test.ts, tsconfig.json)
3059192f feat(web):  resolve the zone aliases in vite and vitest        (2 files)
ef37f773 refactor(web): move src into the app/features/shared zones
607b9914 build(web): point the start plugin at the src/app source root
f6a74c30 refactor(web): route every cross-zone import through a zone alias
9fe33b12 test(web):  repoint the app-wide policy specs at the zone layout
eedddfef feat(auth): add the zone alias map and its tsconfig pin test   ← copy of 2434359d
2dff0701 feat(auth): resolve the zone aliases in vite and vitest        ← copy of 3059192f
83b1e4f4 refactor(auth): move src into the app/features/shared zones
9e7a1392 build(auth): point the start plugin at the src/app source root
3888565f refactor(auth): route every cross-zone import through a zone alias
0e3b819f test(auth): repoint the app-wide policy specs at the zone layout
f0b2abf4 test(web):  follow wallow-auth's request-origin copy into its shared zone
82ddeca9 test:       enforce the app zone import DAG                    (2 files × 329 identical lines)
96ff3277 docs:       describe the three-zone app layout and its import DAG
1585962c test(ui):   point the CSS-entry guard at the zoned styles.css
```

**Verdict on the hypothesis: refuted for the aliases.** The alias/zone layer is a single
deliberate 2026-07-30 restructure, applied to wallow-web and then replayed commit-for-commit on
wallow-auth. Its duplication is the *replay*, not accretion. The hypothesis is **confirmed** for
`node:async_hooks` (§3.2, born in BFF SSR-context plumbing) and **partly** for the
`use-sync-external-store` shim (§3.1, born in the BFF spike, re-hit as stores were added).
`ssr.noExternal` (§3.3) and `copyPublicDir` (§3.8) are unrelated to auth.

### 3.7 `extraBrowserOptimizeDeps` — why each entry is listed

The general mechanism (`packages/testing/src/browser-optimize-deps.ts:1-9`): left to on-the-fly
discovery, Vite pre-bundles mid-run and reloads; the reload drops the test runner
("Vitest failed to find the runner" / "Vitest unexpectedly reloaded a test"). In
`packages/ui` it is worse — a Base UI subpath discovered late is pre-bundled into a chunk
carrying **its own copy of React**, and the first render dies on
`Cannot read properties of null (reading 'useRef')` (`packages/ui/vitest.config.ts:19-26`).

wallow-web's list (`apps/wallow-web/vitest.config.ts:37-71`), entry by entry:

| Entry | Documented reason |
|---|---|
| `react`, `react/jsx-*-runtime`, `react-dom` | render baseline beyond the preset's four |
| `@bc-solutions-coder/query`, `@bc-solutions-coder/auth` | **linked workspace packages are not pre-bundled by default**; naming them inlines react-query's runtime into one chunk so the browser graph has exactly one `QueryClientProvider` context — two surfaces as "No QueryClient set" (`:42-47`) |
| `@tanstack/react-query` | **kept as policy, and it does not resolve.** The manifest dropped react-query, so Vite logs one "Failed to resolve dependency" line per run; the actual pre-bundling is done by the facade entry above (`:50-53`). This is a knowingly-dead entry. |
| `@tanstack/react-router`, `@tanstack/react-form`, `zustand`, `lucide-react` | render/runtime libs the app mounts |
| `zod` | arrives with `@bc-solutions-coder/forms`; it is the **schema module**, not the form package, the scanner misses on the first pass (`:59-61`) |
| 5 × `@base-ui/react/*` | Base UI is reached through per-component **subpaths**, and Vite pre-bundles a subpath only when named — the package root does not cover them (`:63-65`) |

wallow-auth's `:21-41` adds the sharpest note: it lists TanStack Query **only** under the facade
name, never the react-query specifier, because the app no longer declares react-query and under
pnpm's strict `node_modules` it cannot resolve that specifier at all — *"an unresolvable
`optimizeDeps` entry is only a WARNING, after which Vite pre-bundles nothing and the discovery
reload comes back with a config that looks correct. packages/forms hit this first."*

Note the inconsistency: wallow-auth deliberately omits the unresolvable `@tanstack/react-query`
entry; wallow-web (`:54`) and `packages/testing` (`:41`) deliberately keep it and eat the warning.

### 3.8 `client: { build: { copyPublicDir: true } }`
`apps/wallow-web/vite.config.ts:61-69`, `apps/wallow-auth/vite.config.ts:76-84`,
`apps/examples/minimal-app/vite.config.ts`.

`nitro/vite` assumes it alone fills `.output/public` and does `config.build.copyPublicDir ??=
false` on the **client** environment. That silently drops the brand assets `wallowStyles()`
points `publicDir` at, so `/piggy-icon.svg` (favicon *and* the navbar/attribution mark) 404s in
the **built** output while the dev server — which serves `publicDir` itself — looks fine. Because
nitro uses `??=`, spelling it out wins.

**History**: `git log -S 'copyPublicDir'` → `01fa5ec0 feat(web): retire the Blazor Wallow.Web app
for the React port`, `cc8311e3`. Unrelated to auth; it is a nitro/Vite-environments interaction.
Pinned by `brand-assets.test.ts:100-108` explicitly *because* no dev-server check can catch it.

### 3.9 Two more undocumented-in-the-briefing items

- **`apps/wallow-auth`'s `BASE_PATH` triangle** (`vite.config.ts:30-43,107,114`): the prefix must
  be spelled three ways — Vite `base` (trailing slash), Start `router.basepath` (no trailing
  slash), nitro `baseURL`. Without nitro's, the server keeps serving `.output/public` at the
  root and every prefixed asset 404s: the page renders but never hydrates. It is a **build**
  input, not runtime, so the Dockerfile takes it as an `ARG` promoted to `ENV`. Pinned by
  `apps/wallow-auth/src/base-path-wiring.test.ts`.
- **The deliberate absence of `vite: { installDevServerMiddleware }`** in all three apps' vite
  configs (`wallow-web:22-25`, `wallow-auth:22-25`, `minimal-app`): the Start plugin auto-detects
  a non-runnable SSR environment — exactly what `nitro()` installs — and skips its dev
  middleware; forcing the option on makes `vite dev` fail to boot.

---

## 4. Tests that constrain the design

| Test | Invariant it protects | Genuine requirement? | Survives a best-practice refactor? |
|---|---|---|---|
| `apps/*/src/alias-map.test.ts` | tsconfig `paths` ≡ `aliasDirs`; and both build configs really import the map | **No — self-referential.** It exists solely to pin a hand-written mirror that a single-source scheme would delete. Its `:67-69` case ("vite.config.ts imports the alias map") is a test that the current *implementation shape* is the current implementation shape. | **Delete it** under any scheme with one source of truth. If a mirror survives, keep only the equality case (`:47-59`); drop `:43-45` (hard-codes the three keys — see §2a) and `:67-69`. |
| `apps/*/src/zone-dag.test.ts` | (a) cross-zone imports resolve legally; (b) features are barrel-only; (c) no feature↔feature; (d) nothing reaches back into `app/`; (e) `shared/` reaches no feature; (f) nothing escapes `src/` | **(a)–(f) yes, genuinely architectural.** But the *sub-rule* at `:238-249` — "spell every cross-zone import as an **alias**, never a relative hop" — is a **style** rule bolted onto a structural one. | The DAG survives any scheme. The "must be an alias" clause is the one that would need rewriting if specifiers changed shape (e.g. `#app/*` subpath imports, or package-per-zone). `targetOf()` (`:162-195`) hard-codes the three prefixes and would silently stop policing any new/renamed zone (§2a) — that is an artifact, not a requirement. |
| `apps/*/src/feature-barrels.test.ts` | every `features/<x>/` has an `index.ts`; it is re-exports only, no `export *`, reaching only `./` | **Yes.** Independent of how the alias is spelled — it reads the barrel file itself. | Unaffected. |
| `apps/*/src/feature-barrels.browser.test.tsx` | each barrel actually mounts in Chromium | Yes. | Unaffected. |
| `apps/*/src/docker-workspace-copies.test.ts` | Dockerfile COPY list ≡ manifest `workspace:*` deps, with correct ordering around install/build | **Yes, load-bearing** — the drift signature is `UNRESOLVED_IMPORT` minutes into CI and never in `pnpm check` (`:8-17`). | Unaffected by aliases, but **would need extending** if `aliases.ts` (or a shared build-config package) moved outside the app directory — see §1h. |
| `apps/*/src/brand-assets.test.ts:87-96` | `srcDirectory: "src/app"` and `importProtection: { include: ["src/**"] }` are both present, asserted on **config text** | **Yes, the single most load-bearing guard here.** The failure it prevents (server-only `redis`/`openid-client` reaching a client bundle) is invisible until it ships. | Survives, but it is a *regex over source text* — the most brittle possible mechanism. Any reformat of the vite config breaks it. A best-practice implementation could assert the resolved plugin options instead. |
| `apps/*/src/brand-assets.test.ts:100-108` | `environments.client.build.copyPublicDir === true` | Yes (§3.8). Asserts the **imported config object**, not text — strictly better than the sibling above. | Unaffected. |
| `packages/ui/src/core/package-scaffold.test.ts:180-189` | both apps' Tailwind entries import `@bc-solutions-coder/ui/source.css` | Genuine (it is what makes ui sources get scanned) but **hard-codes `apps/wallow-auth` / `apps/wallow-web` and the `src/app/styles.css` path**. `1585962c` exists only because the zone move broke it — it failed with ENOENT. | Survives; needs the path updated on any layout change, and enumerates apps by name. |
| `apps/*/src/features-api-seam.test.ts` | every data-consuming module reaches the API through its feature's `api.ts` seam | Yes. But `:441-456` carries **special-case logic for alias vs relative seam spellings** (`join(dirname(path), "@features/login")` is a nonsense path, so alias seams are string-compared instead). | Survives; the alias branch is a direct artifact of the current spelling. |
| `apps/*/src/query-facade.test.ts`, `shared-auth.test.ts`, `shared-current-user.test.ts`, `styling.test.ts`, `service-identity.test.ts` | various; each hard-codes zoned paths like `src/app/router.tsx`, `src/shared/lib/…`, `src/features/<x>/components/…` as **string literals** | The invariants are genuine; the **path literals** are not. | Each needs a mechanical path update on any layout change. `shared-auth.test.ts:55` already carries a comment about `src/lib/` → `src/shared/lib/`. |

**Honest summary of load-bearing vs self-referential:**

- **Load-bearing, keep at any cost:** `brand-assets.test.ts` (both cases), `zone-dag.test.ts`
  rules (a)–(f), `docker-workspace-copies.test.ts`, `feature-barrels*.test.ts`.
- **Self-referential — exists only to pin a mirror best practice would delete:**
  `alias-map.test.ts` in its entirety.
- **Genuine invariant, artifact mechanism:** `zone-dag.test.ts`'s alias-spelling clause and its
  hard-coded three prefixes; `features-api-seam.test.ts`'s alias branch;
  `brand-assets.test.ts`'s text-regex assertion; every hard-coded `src/<zone>/…` path literal
  across the policy specs.

---

## 5. Real usage data

Measured with `rg` over `apps/<app>/src/**/*.{ts,tsx}`. Counts include the one self-reference
inside each `zone-dag.test.ts` (the guard names the prefixes it polices).

| Alias | wallow-web files / occurrences | wallow-auth files / occurrences |
|---|---|---|
| `@app/` | 10 / 10 | 20 / 20 |
| `@features/` | 9 / 10 | 18 / 20 |
| `@shared/` | 47 / 68 | 60 / 64 |
| **total ts/tsx under `src/`** | **146** | **140** |

**Finding: `@app/` has zero product usage in either app.** Excluding each app's own
`zone-dag.test.ts:163`, every remaining `@app/` import is in a `*.test.tsx`, and all of them are
the same specifier — `import { getRouter } from "@app/router"` (9 of 9 in wallow-web; 19 of 19 in
wallow-auth are the `@app/router` / `@app/routes/<name>` spec pattern). This follows from the DAG
itself: `features/` and `shared/` may not reach `app/` at all (except specs, per
`SPEC_MAY_REACH_APP` at `zone-dag.test.ts:48`), and files *inside* `app/` reach each other
relatively. `@app` is structurally a **test-only alias**.

`@features/` is used exclusively as a bare barrel specifier — 9 of 10 in wallow-web and 19 of 20
in wallow-auth match `"@features/<name>"` with no subpath (the remainder is the guard's own
literal). Consistent with `zone-dag.test.ts:172-183` banning deep feature imports.

`@shared/` is where the volume is. wallow-web's top specifiers are dominated by test harnesses:
`@shared/testing/style-contract` (21), `@shared/testing/harness-routes` (14),
`@shared/testing/invalidation` (11), `@shared/testing/catalog-select` (9) — **55 of 68
occurrences are `@shared/testing/*`**. Only 13 are product modules (`@shared/lib/site-links` ×4,
`@shared/components/*`, `@shared/lib/request-origin`, `@shared/lib/error-text`).

**DAG violations today: zero.** No `from "../../../…"` specifier exists anywhere under either
app's `src/`, and `zone-dag.test.ts:238-309` is green on `main`.

**Ambiguity under a different scheme:** the only genuine collision hazard is the prefix-matching
one the code already guards against — a bare `@app` alias key would swallow a hypothetical
`@application` (`aliases.ts:24-28`), and `use-sync-external-store/shim` would swallow
`…/shim/with-selector` (`vite.config.ts:44-50`). Note the tsconfig mirror does **not** have this
problem (`@app/*` requires the slash by construction), so the trailing-slash trick exists purely
to make Vite's prefix semantics match TypeScript's glob semantics — one more place the two
mechanisms have to be manually reconciled.

---

## 6. Structural observations (facts, not proposals)

1. `aliases.ts` is byte-identical across the two apps; so are `alias-map.test.ts` and
   `zone-dag.test.ts` (329 lines each, `82ddeca9` added both in one commit). ~430 duplicated
   lines of alias/DAG machinery per app.
2. The `packages/testing` preset — the workspace's one shared build-config seam — deliberately
   does **not** carry aliases (`aliases.ts:11-12`), and its `nodeProjectOverrides` escape hatch
   is currently used by nobody and documented against a config that no longer exists
   (`vitest-projects.ts:16-19` vs `apps/wallow-web/vitest.config.ts:23-27`).
3. The browser project of `createVitestProjects` has **no** override channel at all, which is why
   both apps hand-splice `resolve.alias` onto the returned object.
4. `packages/sdk/tsconfig.json` is the one workspace member that does not extend
   `tsconfig.base.json`.
5. Node's `"imports"` (subpath imports) — the platform-native answer to intra-package aliasing,
   understood by TypeScript, Vite, Vitest, Node and oxlint without a mirror — is unused
   anywhere in the workspace.
6. `@tanstack/react-query` is listed in `optimizeDeps.include` in two places
   (`apps/wallow-web/vitest.config.ts:54`, `packages/testing/vitest.config.ts:41`) where it is
   known to be unresolvable and produces a warning per run, and deliberately omitted in a third
   (`apps/wallow-auth/vitest.config.ts:21-41`) for the same reason.
