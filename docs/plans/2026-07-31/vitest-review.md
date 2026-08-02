**status: active**

# Vitest configuration review — what is baroque, what is load-bearing

Scope: `apps/*/vitest.config.ts`, `packages/*/vitest.config.ts`, `packages/testing/src/**`,
the per-app `vitest.setup.ts` / `vitest-styles.css`. Every claim below is tied to an
experiment that was actually run on this machine on 2026-07-31.

## Installed versions (read from `node_modules`, not from a manifest range)

| Package                     | Version |
| --------------------------- | ------- |
| `vitest`                    | 4.1.10  |
| `@vitest/browser`           | 4.1.10  |
| `@vitest/browser-playwright` | 4.1.10  |
| `vitest-browser-react`      | 2.2.0   |
| `vite`                      | 8.1.4 (Rolldown) |
| `@storybook/addon-vitest`   | 10.5.5  |

## Experiment protocol and its caveats

`packages/ui` was the instrument. Vite's dep cache masks everything interesting, so each
run did `rm -rf packages/ui/node_modules/.vite` first — a config-hash change alone is NOT
a cold start, and the first (misleading) result in this review came from exactly that
mistake.

Two honesty notes:

- **The working tree was being edited by another agent throughout.** `drawer.test.tsx` was
  rewritten at 12:39:00, between the first two runs, which produced three phantom
  `text-lg`/`text-xl` failures that had nothing to do with any config. Some runs also show
  `dist-structure.test.ts` failing against a momentarily-empty `packages/ui/dist`. Those are
  called out where they appear and are excluded from every verdict.
- **The first "the list is unnecessary!" result was wrong** and is reported below as such.

---

## 1. The 40-entry `optimizeDeps.include` lists

**The claim** (`packages/ui/vitest.config.ts:19-30`, repeated in `packages/forms` and both
apps): the list "is not an optimisation — it is required". Left to discovery, Vite
pre-bundles a Base UI subpath with its own React copy → `Cannot read properties of null
(reading 'useRef')`, or a mid-run reload drops the runner.

### Experiment 1a — remove the lists, warm-ish cache (INVALID, reported for honesty)

Emptied `extraBrowserOptimizeDeps` and deleted the storybook project's `optimizeDeps`, then
ran without purging `node_modules/.vite`. Vite logged `Re-optimizing dependencies because
vite config has changed` and the suite came back **119/119 files, 1515/1515 tests green** in
28s. This looked like a clean deletion. It was not a cold start — the on-disk chunks from
previous runs were still there. Do not trust this number.

### Experiment 1b — cold cache, list present (control)

`rm -rf packages/ui/node_modules/.vite` before each of 2 runs:

| Run | Files | Tests | Wall |
| --- | ----- | ----- | ---- |
| 1   | 119 passed | 1515 passed | 16.4s |
| 2   | 119 passed | 1515 passed | 17.5s |

### Experiment 1c — cold cache, no list at all

Same protocol, `extraBrowserOptimizeDeps: []` and no storybook `optimizeDeps`:

| Run | Files | Tests | Wall |
| --- | ----- | ----- | ---- |
| 1   | **36 failed** / 83 passed | **125 failed** / 1101 passed | **281.8s** |
| 2   | 119 passed | 1515 passed | 19.8s |

Run 1's failure signature, by frequency:

```
  75+  Error: locator.click: Timeout 14.9s exceeded.
   17  Error: Matcher did not succeed in time.
   12  Error: Failed to import test file .../accordion.test.tsx
        Caused by: TypeError: Failed to fetch dynamically imported module:
        http://localhost:63316/.../accordion.test.tsx?import&browserv=...
    1  Error: locator.hover: Timeout 29.9s exceeded.
```

Both the `browser` and the `storybook` projects were hit. Every popup-family component
(`menu`, `menubar`, `select`, `combobox`, `popover`, `tooltip`, `toast`, `context-menu`,
`navigation-menu`, `preview-card`, `dialog`, `drawer`, `alert-dialog`, `autocomplete`) failed.

**Verdict on the mechanism: KEEP. The workaround is still required under Vite 8 / Vitest
4.1.10.** One in two cold runs without it is catastrophic, and it is 16x slower when it
goes wrong. This is the experiment the user's suspicion was aimed at, and it came back
against the suspicion.

Two details worth recording because they contradict the comments in the code:

- The symptom has **changed**. `Vitest unexpectedly reloaded a test` appeared **zero** times
  (that warning is emitted by `@vitest/browser/dist/index.js:7921` when Vite logs `optimized
  dependencies changed. reloading`), and `useRef` of null never appeared either. The modern
  failure is `Failed to fetch dynamically imported module` on the spec file itself plus mass
  actionability timeouts. The comments describe a Vite 5/6-era symptom.
- The obvious generic fix is **already applied by Vitest and is not sufficient**.
  `@vitest/browser/dist/index.js:1048` sets `optimizeDeps.entries` to *every browser test
  file plus every setup file*, so the scanner does crawl all the specs; and Vite's
  `holdUntilCrawlEnd` already defaults to `true`. Neither `optimizeDeps.entries` nor
  `holdUntilCrawlEnd` is a lever left to pull here — they are on, and cold discovery still
  loses half the time. I could not isolate the root cause (best hypothesis: a race between
  the crawl and the storybook project's second Vite server); the empirical result stands on
  its own.

### Experiment 1d — replace the hand-maintained list with ONE glob

`optimizeDeps.include` supports a trailing glob for deep imports
(<https://vite.dev/config/dep-optimization-options#optimizedeps-include>). In vite@8.1.4,
`expandGlobIds()` (`dist/node/chunks/node.js:31005`) matches the pattern against the
**package's `exports` map keys**, which is exactly the set of Base UI subpaths.

Replaced the 38-entry `baseUiSubpaths` array with `["@base-ui/react/*"]`, kept
`recipeRuntime`. Three cold runs:

| Run | Browser + storybook | Wall | Notes |
| --- | ------------------- | ---- | ----- |
| 1   | all green | 14.8s | 1 node guard + 3 stale-`dist` failures (see below) |
| 2   | all green | 12.7s | same |
| 3   | all green | 13.0s | 1 node guard failure only |

Indistinguishable from the hand-maintained list (16.4 / 17.5s), and the pre-bundle actually
happened — reading the resulting `_metadata.json`:

```
total optimized: 58
base-ui subpaths: 45          (the hand list names 39)
has root @base-ui/react: true
```

The 3 `dist-structure.test.ts` failures in runs 1-2 were the concurrently-running agent's
empty `packages/ui/dist`; they cleared by run 3 without any config change.

The 1 persistent failure is real and is the cost of this change:

```
FAIL |node| src/components/list-row/list-row.composition.test.ts
     > registers the use-render subpath for both browser vitest projects
```

`list-row.composition.test.ts:102-110` reads `vitest.config.ts` as text and asserts the
literal string `"@base-ui/react/use-render"`. It must be deleted or rewritten against the
glob.

**Verdict: REPLACE.** `baseUiSubpaths` → `["@base-ui/react/*"]` in both `packages/ui` and
`packages/forms`. This deletes the per-component ritual ("EVERY component task must append
its own subpath here") from `packages/ui/CLAUDE.md`, `packages/forms/CLAUDE.md` and the
add-a-component checklist. Keep `recipeRuntime` (`class-variance-authority`,
`tailwind-merge`) — package-wide, does not grow, and there is nothing to glob.

`noDiscovery: true` was **not** adopted: it would make an unresolvable entry fatal rather
than latent, which sounds attractive, but see finding 2 — several entries are unresolvable
today, so turning it on is a follow-up, not part of this change.

---

## 2. Dead entries: 8 of wallow-web's 22, and 1 in the SHARED baseline

`optimizeDeps.include` entries that Vite cannot resolve are a **warning**, after which Vite
pre-bundles nothing — the exact hazard `packages/forms/src/core/browser-deps.test.ts` was
written to catch. `packages/forms` has that guard. Nobody else does. Running each browser
project and collecting the warnings:

| Package | Unresolvable entries |
| ------- | -------------------- |
| `apps/wallow-web` | `@base-ui/react/{autocomplete,checkbox,combobox,navigation-menu,select,toggle,toggle-group}` + `@tanstack/react-query` — **8 of 22** |
| `packages/ui` | `vitest-browser-react` — and this one comes from the SHARED `browserOptimizeDepsBaseline` |
| `apps/wallow-auth`, `packages/forms`, `packages/testing` | none |

Cause is pnpm's strict `node_modules`: `apps/wallow-web` does not declare `@base-ui/react`
(confirmed: `require('./apps/wallow-web/package.json').dependencies['@base-ui/react']` →
undefined), and `packages/ui` does not declare `vitest-browser-react`.

wallow-web's dep cache nonetheless contains **all 39** Base UI subpaths — they arrive by
automatic discovery through the linked `@bc-solutions-coder/ui`. So the seven hand-listed
subpaths in `apps/wallow-web/vitest.config.ts:73-83`, whose comment insists "Vite
pre-bundles a subpath only when it is named", are provably contributing nothing.

**Verdict:**
- wallow-web: either add `@base-ui/react` to `devDependencies` and use the glob, or delete
  the 7 entries. Do not leave them as decoration.
- `packages/ui`: add `vitest-browser-react` to `devDependencies`, or change the shared
  baseline to name `@bc-solutions-coder/testing/render` (the specifier ui actually imports).
- Lift `browser-deps.test.ts`'s resolvability check into `packages/testing` so every
  consumer inherits it. This is the one guard in the tree that is worth *more* code, not less.

---

## 3. `packages/ui` repeating the list twice (browser + storybook)

Still true, and still unavoidable: `test.projects` entries get one Vite server each, so the
`storybook` project has its own dep cache and cannot share the `browser` project's. There is
no supported way to share one cache between two Vitest projects.

**Verdict: KEEP the structure, but the pain evaporates** — with finding 1d both lists become
the same two shared constants (`baseUiGlob`, `recipeRuntime`) and neither grows per component.

---

## 4. `nodeTsxSpecs` → a filename convention

Current hand-maintained entries: **4 across the workspace.**

| App | Entry | Conforms to `*.ssr.test.tsx`? |
| --- | ----- | ----------------------------- |
| wallow-web | `src/app/routes/index.test.tsx` | no |
| wallow-web | `src/app/routes/dashboard/route.test.tsx` | no |
| wallow-web | `src/shared/lib/use-is-desktop.ssr.test.tsx` | **yes** |
| wallow-auth | `src/app/routes/index.test.tsx` | no |

So **3 files need renaming** and the convention half-exists already.

Caveat on the name: `use-is-desktop.ssr.test.tsx` really does render through
`react-dom/server`, but the two `routes/index.test.tsx` files and `dashboard/route.test.tsx`
assert a route's `beforeLoad` redirect and render nothing at all. `.ssr.` is a slight
stretch for those; `*.node.test.tsx` is the more honest name and costs one extra rename
(4 instead of 3). Either way it becomes a glob and the option disappears from both app
configs.

**Verdict: REPLACE with a convention.** In `createVitestProjects`, default
`nodeTsxSpecs` to `["src/**/*.ssr.test.tsx"]` (node `include` gains it, browser `exclude`
gains it) and keep the option only as an escape hatch. Both app configs lose the array.

---

## 5. The `node:async_hooks` browser shim — VERIFIED STILL NEEDED

Removed the `resolve.alias` from wallow-web's browser project and ran a spec that imports
`@app/router`:

```
FAIL |browser (chromium)| src/app/routes/bff-demo.test.tsx
Error: Failed to import test file .../bff-demo.test.tsx
Caused by: TypeError: import_browser_external_node_async_hooks.AsyncLocalStorage is not a constructor
 ❯ .../@tanstack+start-storage-context@1.167.17/dist/esm/async-local-storage.js:5:68
```

The installed `@tanstack/start-storage-context@1.167.17` still does
`import { AsyncLocalStorage } from "node:async_hooks"` and `new AsyncLocalStorage()` at
module scope (`dist/esm/async-local-storage.js:1,5`).

**Verdict: KEEP,** exactly as written. Roughly 15 browser specs import `@app/router`.

---

## 6. `ssr.noExternal` for linked workspace packages

Removed `ssr.noExternal` from `apps/wallow-web/vitest.config.ts` and ran the node project:

| Config | Result |
| ------ | ------ |
| with `ssr.noExternal` | 35 files / 498 tests passed |
| without | 34 files passed, **1 failed** |

The single failure is `src/query-facade.test.ts > inlines the facade for the node project
instead of externalizing it` — a **config-text guard** asserting the regex
`/noExternal:\s*\[[^\]]*"@bc-solutions-coder\/query"/` against the config source. Every
functional spec, including the SSR route specs, passed without the option.

So today the option is inert for correctness and only its own guard makes it look
load-bearing.

**Verdict: KEEP for now, and do NOT delete it as part of the source-condition exports work.**
The prompt's guess was that source-condition exports would delete this option; the
dependency runs the other way. Today the linked packages externalize to built `dist/` JS,
which Node loads fine — hence "inert". If `exports` starts resolving those packages to
**TypeScript source**, Vite must transform them and `ssr.noExternal` becomes genuinely
required. Re-run this experiment after that change rather than reasoning about it.

---

## 7. `test.resolve` being per-project

The comment ("`resolve` is PER PROJECT — a root-level `resolve` is NOT inherited by
`test.projects`") is **true but stale**. Vitest's docs
(<https://vitest.dev/guide/projects#configuration>) state: "None of the configuration options
are inherited from the root-level config file… Additionally, you can use the `extends`
option to inherit from your root-level configuration. All options will be merged."

Experiment: hoisted `resolve: { tsconfigPaths: true }` to the root of
`apps/wallow-auth/vitest.config.ts` and put `extends: true` on both projects.

```
Test Files  66 passed (66)
     Tests  1367 passed (1367)
```

**Verdict: REPLACE.** State `resolve` once at root, `extends: true` per project. This also
gives root-level `plugins` and `ssr` a defined meaning instead of the current ambiguity.

---

## 8. Duplication across the 10 configs — what can actually move

Current sizes: wallow-web 134 lines, ui 123, forms 85, wallow-auth 78, testing 59,
minimal-app 18, auth 19, query 14, sdk 13, styles 13. Preset: `vitest-projects.ts` 136 +
`browser-optimize-deps.ts` 31.

**Leave alone:** `packages/{auth,query,sdk,styles}` are 13-19 lines of
`{ environment: "node", include: ["src/**/*.test.ts"] }`. Abstracting those costs more than
it saves.

**Genuinely duplicated, and movable:**

1. The browser-styling override block — `plugins: wallowStyles()` +
   `test: { ...browser.test, setupFiles: ["./vitest.setup.ts"] }` — appears **verbatim 3x**
   (wallow-web, wallow-auth, forms), each wrapped in 15-20 lines of comment.
2. `vitest.setup.ts` is **byte-identical in all 3** apart from comments: two import lines.
3. `vitest-styles.css` is **byte-identical in all 3** apart from comments: two `@import`s
   plus `@source "./src"`.
4. `resolve: { tsconfigPaths: true }` repeated per project in both apps (finding 7).

The CSS file cannot be hoisted — Tailwind v4 resolves `@source` relative to the declaring
stylesheet, as `packages/testing/CLAUDE.md` already records. The **setup file can**:
`setupFiles: ["./vitest.setup.ts"]` is resolved against each project's own root, so the
preset can name it and each package keeps its own one-line file.

Proposed preset options (keeps `packages/testing` styling-agnostic — no new dependency on
`@bc-solutions-coder/styles`, the consumer still calls `wallowStyles()`):

```ts
export interface VitestProjectsOptions {
  nodeTsxSpecs?: string[];            // defaults to ["src/**/*.ssr.test.tsx"]
  extraBrowserOptimizeDeps?: string[];
  nodeProjectOverrides?: Record<string, unknown>;
  /** Vite plugins for the browser project only (e.g. `wallowStyles()`). */
  browserPlugins?: PluginOption[];
  /** Browser-project setup files, resolved per project root. Default: none. */
  browserSetupFiles?: string[];
}
```

Estimated result: wallow-web 134 → ~45, ui 123 → ~55, forms 85 → ~30, wallow-auth 78 → ~25.

---

## 9. The config-text guard layer — the real constraint on all of this

**11 spec files read a `vitest.config.ts` as text** and assert against its source. Two of
them actively block the changes above:

- `packages/ui/src/components/list-row/list-row.composition.test.ts` — blocks the glob (§1d).
- `apps/wallow-web/src/query-facade.test.ts` — blocks removing `ssr.noExternal` (§6).

Others to move in the same commit: `packages/forms/src/core/browser-deps.test.ts`,
`packages/testing/src/vitest-projects.test.ts`, and the two
`shared/testing/browser-styles-wiring.test.ts` files.

This layer is why the configs feel hardcoded: the comments and the guards make each entry
look mandatory, and no experiment had been re-run since the Vite 8 upgrade. Half of them
were right.

---

## Proposed target-state configs

### `packages/testing/src/vitest-projects.ts` (changed parts only)

```ts
export interface VitestProjectsOptions {
  /**
   * Pure-logic / SSR `*.test.tsx` specs that belong on node, not in Chromium.
   * Defaults to the `*.ssr.test.tsx` convention; pass an explicit list only to
   * override it.
   */
  nodeTsxSpecs?: string[];
  extraBrowserOptimizeDeps?: string[];
  nodeProjectOverrides?: Record<string, unknown>;
  /** Vite plugins for the BROWSER project only (e.g. `wallowStyles()`). */
  browserPlugins?: PluginOption[];
  /** Browser-project setup files; resolved against each project's own root. */
  browserSetupFiles?: string[];
}

const SSR_SPEC_GLOB = "src/**/*.ssr.test.tsx";

export function createVitestProjects(options: VitestProjectsOptions = {}): VitestProjectsPair {
  const {
    nodeTsxSpecs = [SSR_SPEC_GLOB],
    extraBrowserOptimizeDeps = [],
    nodeProjectOverrides = {},
    browserPlugins = [],
    browserSetupFiles = [],
  } = options;

  const node: VitestNodeProject = {
    test: {
      name: "node",
      environment: "node",
      include: ["src/**/*.test.ts", ...nodeTsxSpecs],
      exclude: [...configDefaults.exclude],
      testTimeout: 60_000, // cold route-graph import; measured at 19s for wallow-auth
    },
  };

  const browser: VitestBrowserProject = {
    plugins: browserPlugins,
    optimizeDeps: { include: mergeOptimizeDeps(extraBrowserOptimizeDeps) },
    test: {
      name: "browser",
      include: ["src/**/*.test.tsx"],
      exclude: [...configDefaults.exclude, ...nodeTsxSpecs],
      setupFiles: browserSetupFiles,
      browser: {
        enabled: true,
        provider: playwright(), // Vitest 4 factory, NOT the v3 "playwright" string
        headless: true,
        instances: [{ browser: "chromium" }],
      },
    },
  };

  return { node: mergeConfig(node, nodeProjectOverrides) as VitestNodeProject, browser };
}
```

### `packages/ui/vitest.config.ts` (123 → 55)

```ts
import { fileURLToPath } from "node:url";

import { createVitestProjects } from "@bc-solutions-coder/testing";
import { storybookTest } from "@storybook/addon-vitest/vitest-plugin";
import { defineConfig } from "vitest/config";

/**
 * Three projects: node (pure-logic `*.test.ts`), browser (component `*.test.tsx`
 * in headless Chromium), storybook (`@storybook/addon-vitest` running every story
 * as a test case in that same Chromium, with the real Tailwind pipeline).
 *
 * `optimizeDeps.include` is REQUIRED, not an optimisation, and was re-verified on
 * vite@8.1.4 / vitest@4.1.10: with a purged `node_modules/.vite`, one cold run in
 * two without it fails catastrophically (36 files / 125 tests, 282s against 16s),
 * with specs dying on `Failed to fetch dynamically imported module`. Vitest already
 * sets `optimizeDeps.entries` to every spec and Vite's `holdUntilCrawlEnd` is
 * already on, so there is no generic lever left — the deps must be named.
 *
 * They are named by GLOB, not one line per component: vite's `expandGlobIds`
 * matches the pattern against @base-ui/react's `exports` keys, which pre-bundles
 * all 45 subpaths. Adding a component therefore no longer touches this file.
 *
 * The two lists are repeated for the storybook project because a Vitest project
 * gets its own Vite server and its own dep cache; there is no way to share one.
 */
const baseUiSubpaths = ["@base-ui/react/*"];

/** The recipe runtime every component pulls in via `*.styles.ts` and `core/cn.ts`. */
const recipeRuntime = ["class-variance-authority", "tailwind-merge"];

const preBundled = [...baseUiSubpaths, ...recipeRuntime];

const { node, browser } = createVitestProjects({ extraBrowserOptimizeDeps: preBundled });

const storybook = {
  plugins: [storybookTest({ configDir: fileURLToPath(new URL(".storybook", import.meta.url)) })],
  optimizeDeps: { include: preBundled },
  test: {
    name: "storybook",
    browser: {
      enabled: true,
      provider: browser.test.browser.provider,
      headless: true,
      instances: [{ browser: "chromium" as const }],
    },
  },
};

export default defineConfig({ test: { projects: [node, browser, storybook] } });
```

Requires: add `vitest-browser-react` to `packages/ui` `devDependencies` (§2), and delete the
`use-render` assertion in `list-row.composition.test.ts` (§1d).

### `apps/wallow-auth/vitest.config.ts` (78 → 25)

```ts
import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * The shared node + headless-Chromium split, plus this app's two knobs.
 *
 * `*.ssr.test.tsx` is the node-project convention (renders through
 * `react-dom/server` or asserts a `beforeLoad` redirect, never mounts a DOM);
 * everything else `*.test.tsx` mounts a component and runs in Chromium.
 *
 * `wallowStyles()` + ./vitest.setup.ts are not cosmetic: a ui control gets its BOX
 * from a Tailwind utility, so with no stylesheet a catalog checkbox measures 0x0
 * and a click hangs to Playwright's actionability timeout; the theme half gives
 * colour utilities their values, without which every colour computes transparent.
 *
 * The optimizeDeps entries name the query facade (never the react-query specifier
 * it re-exports — under pnpm this app cannot resolve that, and an unresolvable
 * entry is a silent warning that pre-bundles nothing) and `zod`, which arrives
 * with @bc-solutions-coder/forms as a schema module the scanner misses.
 */
const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth", "zod"],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  resolve: { tsconfigPaths: true },
  test: {
    projects: [
      { ...node, extends: true },
      { ...browser, extends: true },
    ],
  },
});
```

Requires renaming `src/app/routes/index.test.tsx` → `index.ssr.test.tsx`.

### `apps/wallow-web/vitest.config.ts` (134 → 45)

```ts
import { fileURLToPath } from "node:url";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * See apps/wallow-auth/vitest.config.ts for the shared shape. wallow-web adds:
 *
 *   - `react-dom/server`, because DashboardLayout.ssr-flash.test.tsx renders the
 *     shell the way the BFF does, INSIDE Chromium, to measure the pre-hydration
 *     paint against real CSS at a real viewport.
 *   - the linked workspace packages (`query`, `auth`) — Vite does not pre-bundle a
 *     link by default, and the pre-bundled chunk is what keeps exactly ONE
 *     react-query copy (two would surface as "No QueryClient set").
 *
 * The `@base-ui/react/*` subpaths this file used to name are GONE: this app does
 * not declare @base-ui/react, so under pnpm every one of them logged "Failed to
 * resolve dependency" and pre-bundled nothing, while discovery through the linked
 * @bc-solutions-coder/ui put all 39 subpaths in the cache anyway (verified in
 * node_modules/.vite/.../\_metadata.json). Ditto `@tanstack/react-query`.
 *
 * `node:async_hooks` stays in `alias`: @tanstack/start-storage-context still runs
 * `new AsyncLocalStorage()` at module scope, the browser project runs without the
 * Start plugin that would compile it away, and vitest externalises the module to a
 * throwing proxy — so without the shim every spec importing `@app/router` dies at
 * import with "AsyncLocalStorage is not a constructor". Re-verified 2026-07-31.
 *
 * `ssr.noExternal` currently changes no test outcome, but keep it: if the linked
 * packages start resolving to TypeScript source, Vite must transform them.
 */
const nodeAsyncHooksShim: string = fileURLToPath(
  new URL("src/shared/testing/node-async-hooks-browser-shim.ts", import.meta.url),
);

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [
    "react",
    "react/jsx-dev-runtime",
    "react/jsx-runtime",
    "react-dom",
    "react-dom/server",
    "@bc-solutions-coder/query",
    "@bc-solutions-coder/auth",
    "@tanstack/react-router",
    "@tanstack/react-form",
    "zustand",
    "lucide-react",
    "zod",
  ],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  resolve: { tsconfigPaths: true },
  ssr: { noExternal: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth"] },
  test: {
    projects: [
      { ...node, extends: true },
      {
        ...browser,
        extends: true,
        resolve: { tsconfigPaths: true, alias: { "node:async_hooks": nodeAsyncHooksShim } },
      },
    ],
  },
});
```

Requires renaming `src/app/routes/index.test.tsx` → `index.ssr.test.tsx` and
`src/app/routes/dashboard/route.test.tsx` → `route.ssr.test.tsx`.

### `packages/forms/vitest.config.ts` (85 → 30)

Same shape as wallow-auth: `baseUiSubpaths` collapses to `["@base-ui/react/*"]`,
`formRuntime` stays as-is, `browserPlugins` / `browserSetupFiles` replace the hand-built
`browser` object.

---

## Summary table

| # | Workaround | Verdict |
| - | ---------- | ------- |
| 1 | `optimizeDeps.include` as a mechanism | **KEEP** — 1 in 2 cold runs fails without it |
| 1d | 38-entry hand-maintained `baseUiSubpaths` | **REPLACE** with `["@base-ui/react/*"]` |
| 2 | wallow-web's 7 Base UI entries + `@tanstack/react-query` | **DELETE** — unresolvable, pre-bundle nothing |
| 2 | `vitest-browser-react` in the shared baseline | **FIX** — unresolvable from `packages/ui` |
| 3 | List repeated for the storybook project | **KEEP** — separate server, separate cache; now 2 constants |
| 4 | `nodeTsxSpecs` hand-list (4 entries) | **REPLACE** with `*.ssr.test.tsx`; 3 renames |
| 5 | `node:async_hooks` browser shim | **KEEP** — reproduced the exact failure |
| 6 | `ssr.noExternal` | **KEEP** — inert today, but source-condition exports make it required |
| 7 | `resolve` repeated per project | **REPLACE** with root `resolve` + `extends: true` |
| 8 | Styling block / setup file duplicated 3x | **MOVE** into the preset (`browserPlugins`, `browserSetupFiles`) |
| 9 | 11 config-text guard specs | **MOVE TOGETHER**; 2 of them block the above |

## Files touched during this review

All experiment edits were reverted; `git status --porcelain` on
`apps/wallow-web/vitest.config.ts`, `apps/wallow-auth/vitest.config.ts` and
`packages/ui/vitest.config.ts` is empty. The pre-existing uncommitted changes elsewhere in
the tree are another agent's and were left untouched.
