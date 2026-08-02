**status: active**

# Vite config review — apps + library packages

Scope: `apps/wallow-web/vite.config.ts`, `apps/wallow-auth/vite.config.ts`,
`apps/examples/minimal-app/vite.config.ts`, `packages/*/vite.config.ts` (7 files),
`packages/styles/src/vite.ts`.

Installed versions actually on disk (`node_modules/.pnpm`):

| Package | Version | Evidence |
| --- | --- | --- |
| `vite` | **8.1.4** | `node_modules/.pnpm/vite@8.1.4_@types+node@24.13.3_...` |
| `rolldown` | **1.1.5** (dep of vite, declared `~1.1.4`) | `node_modules/.pnpm/rolldown@1.1.5`; `vite/package.json` `dependencies.rolldown` |
| `nitro` | **3.0.260610-beta** | `nitro/package.json` |
| `@tanstack/react-start` | 1.168.32 · `start-plugin-core` **1.171.24** · `router-generator` 1.167.21 | `pnpm-workspace.yaml` `catalogs.start`; `.pnpm` dir names |
| `react` | 19.2.7 | `.pnpm/react@19.2.7` |
| `typescript` | 7.0.2 (sdk pins 6.0.3) | `pnpm-workspace.yaml` `catalogs.tooling` / `tooling-tsc6` |

Every app declares `vite ^8.1.4` + `nitro 3.0.260610-beta`; every package declares `vite ^8.1.4`
and builds with `vite build && tsc -p tsconfig.build.json`.

Method note: all "verified" rows below were checked by reading installed `node_modules` source
and/or by building the app, booting `.output/server/index.mjs`, and curling a real SSR route.
Build logs are in the session scratchpad (`.../scratchpad/viterev/`). **All config edits made for
these experiments were reverted; `git status` shows no modification to any file in scope.**

Caveat on the runtime experiments: other agents were concurrently editing `packages/ui` sources
and rebuilding package `dist/` during this session (one build failed mid-run because
`packages/styles/dist/vite.js` was transiently missing). Every result below was re-taken after
restoring a good `dist/`, and each experiment was paired with its own baseline, so the
comparisons are internally consistent.

---

## 1. Classification table

Bucket key: **A** still required · **B** obsolete · **C** right problem, wrong place · **D** unverifiable.

### apps/wallow-web + apps/wallow-auth (the two are near-identical — see §2)

| # | Option / comment | Class | Evidence |
| --- | --- | --- | --- |
| 1 | `server.port` (3000 / 3002 / 3010) | **A** | Playwright configs wait on fixed ports and don't inject `PORT`; `vite dev` defaults to 5173/3000. Not re-tested, but the cost is one line and the failure mode (E2E suite times out) is documented in `.claude/rules/E2E.md`. |
| 2 | `resolve.alias` → `use-sync-external-store/shim` ⇒ `react` (2 anchored regexes) | **A (auth: proven load-bearing) / A-prophylactic (web)** | **Not dead code**: `@base-ui/react@1.6.0` imports the specifier live — `.../@base-ui/react/utils/useIsHydrating.mjs:1` and `unstable-use-media-query/index.mjs:3` (`import { useSyncExternalStore } from 'use-sync-external-store/shim'`), plus `@base-ui/utils`. `pnpm why use-sync-external-store` shows it reached via `@base-ui/react`, `@base-ui/utils`, `@tanstack/react-store`, `zustand`. **Experiment A1**: deleted the two alias entries from wallow-auth, rebuilt, booted `.output/server/index.mjs`, `GET /login` → HTTP 200 but body collapses 9 895 → 2 621 chars (empty shell) and the server logs `Invalid hook call ... TypeError: Cannot read properties of null (reading 'useSyncExternalStore') at useIsHydrating ... at TabsIndicator`, with the second React resolving to `react/cjs/react.development.js:1270`. Restoring the alias restores the 9 895-char render. The comment is exactly right. **Experiment E1** (same deletion on wallow-web): `/bff-demo` still renders identically (3 834 → 3 894 body chars, HTTP 200) — wallow-web's backend-free SSR route does not reach a Base UI store, so its copy of the alias is insurance, not a currently-failing case. |
| 2b | *(new)* residual `__require("react")` from `shim/with-selector` | **A + latent gap** | Even **with** the alias, the shipped server bundle contains `var React = __require("react"); var shim = __require("react");` inside the inlined `use-sync-external-store-shim/with-selector.production.js` (`apps/wallow-auth/.output/server/_ssr/use-app-form-*.mjs:2999-3000`; `__require = createRequire(import.meta.url)` at `_runtime.mjs:24`). `useSyncExternalStoreWithSelector` **is** called from three places in the bundle (`_libs/@tanstack/react-router+[...].mjs:4141`, `_libs/@tanstack/react-form+[...].mjs:534`, `_ssr/use-app-form-*.mjs:3155`), so a second React module instance is one code path away. It does not fire on `/login` or `/bff-demo` today. The alias is therefore a *partial* fix for a rolldown CJS-interop gap (bare `require()` inside a `__commonJSMin` wrapper is not linked to the bundled copy), not a complete one. **File a bead.** |
| 3 | `resolve.tsconfigPaths: true` | **A** | Native in Vite 8 and actively recommended: `vite/dist/node/chunks/node.js:35599` warns if `vite-tsconfig-paths` is present, telling you to use `resolve.tsconfigPaths: true`. Builds resolve `@app/*`/`@features/*`/`@shared/*` with no other alias source. |
| 4 | `resolve.dedupe: ["react", "react-dom"]` | **B (verified no-op today)** | **Experiment A4**: removed from wallow-auth, rebuilt, booted, `GET /login` → HTTP 200, body **9 895 chars — byte-identical to baseline**; exactly one inlined react (`node_modules/react/index.js` header appears once). pnpm installs a single react 19.2.7, so there is nothing to dedupe. Keep only as cheap insurance against a future second react version; it buys nothing now. |
| 5 | `ssr.noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"]` | **A (proven)** | Mechanism unchanged in Vite 8 — docs (<https://vite.dev/config/ssr-options#ssr-noexternal>, <https://vite.dev/guide/ssr#ssr-externals>): "By default, only linked dependencies are not externalized (for HMR)." **Experiment A2**: set `noExternal: []` on wallow-auth, rebuild → **two** react-query graphs in the output (`_ssr/QueryClientProvider-*.mjs` *and* `_libs/tanstack__react-query.mjs` + `_libs/@tanstack/react-router-ssr-query+[...].mjs`). Booted: `GET /login` → HTTP 200, body 2 621 chars (empty shell) and the log shows `Error: No QueryClient set, use QueryClientProvider to set one at useQueryClient ... at useClientBranding ... at LoginRoute`. Exactly the failure the comment describes. |
| 5b | is `resolve.dedupe` / an exports map the "modern" fix instead? | **No — but a 1-line alternative exists** | `resolve.dedupe` cannot fix it (there is one react-query on disk; the split is externalization, not resolution) — confirmed by A4 leaving the duplication untouched. A `packages/query` exports map change cannot fix it either: the package already has a clean `exports` block, and the second copy is pulled by `@tanstack/react-router-ssr-query`, not by the facade. **Experiment A3**: `ssr: { external: ["@bc-solutions-coder/query"], noExternal: [] }` → **one** react-query (`_libs/tanstack__react-query.mjs` only), `GET /login` → HTTP 200, body **9 895 chars, identical to baseline**. This inverts the fix (let nitro own the single graph instead of Vite) and needs no future-consumer enumeration, but it costs dev HMR on the facade. See §3-R2. |
| 5c | `environments.ssr.resolve.noExternal` instead of `ssr.noExternal`? | **No — same thing** | `vite/dist/node/chunks/node.js:35615-35622` merges `config.ssr.{external,noExternal,resolve.*}` into `config.environments.ssr.resolve`, and only when an `ssr` environment exists. The top-level spelling is the supported alias, not a legacy path. |
| 6 | `environments.client.build.copyPublicDir: true` | **A (verified)** | `nitro/dist/vite.mjs:288`, inside `configEnvironment(name, config)` under `if (config.consumer === "client")`: `config.build.copyPublicDir ??= false;`. The `??=` is literally there in the installed beta, so a user-set `true` still wins. (`nitro/dist/_build/vite.env.mjs:16,56` also hard-set `copyPublicDir: false` on the nitro/service environments — irrelevant to the client one.) |
| 7 | `tanstackStart({ srcDirectory: "src/app" })` | **A (verified)** | `start-plugin-core/dist/esm/schema.js:47-49` resolves `routesDirectory` and `generatedRouteTree` as `path.resolve(root, srcDirectory, ...)`; `planning.js:55` does `join(opts.root, opts.startConfig.srcDirectory)` and resolves all four entries against it, with the **router** entry `required: true` (`planning.js:61-82`). `schema.js:141` defaults `srcDirectory` to `"src"`. So the comment's claim ("`routesDirectory: 'src/app/routes'` would resolve to `src/src/app/routes` and the required router entry would still be missing") is accurate. |
| 8 | `importProtection: { include: ["src/**"] }` | **A (verified, exactly as commented)** | `import-protection/adapterUtils.js:22-24`: `else if (config.includeMatchers.length > 0) result = !!matchesAny(...)` / `else if (config.srcDirectory) result = isInsideDirectory(normalizedImporter, config.srcDirectory)`. Without `include`, the importer scope collapses to `srcDirectory` (= `src/app`), silently skipping `src/features/**` and `src/shared/**`. And `import-protection/defaults.js:15-19` confirms the client ruleset is only `specifiers: ["@tanstack/{react,solid,vue}-start/server"]` + `files: ["**/*.server.*"]` — no `node:*`, no `redis`, no SDK path. Both halves of the comment check out. |
| 9 | `router.routeFileIgnorePattern` | **A** | `@tanstack/router-generator/dist/esm/config.js:24` — `routeFileIgnorePattern: z.string().optional()`, **no default**. 21+ specs live under `apps/*/src/app/routes/**` (e.g. `apps/wallow-web/src/app/routes/dashboard/route.guards.test.ts`), so without it they are codegen'd as routes. |
| 10 | wallow-auth `base` / `router.basepath` / `nitro({ baseURL })` trio | **A** | Nitro's default is `baseURL: "/"` (`nitro/dist/vite.mjs:458`); Vite's `base` only rewrites URLs written into HTML. Not exercised (no prefixed build run), but the three-place claim matches the installed defaults. |
| 11 | "There is deliberately NO `vite: { installDevServerMiddleware }`" comment (×3 apps) | **A (verified) but over-documented** | `start-plugin-core/dist/esm/vite/dev-server-plugin/plugin.js:56-60`: when the option is `undefined` it returns early if the server env is not runnable **or** has `dispatchFetch`; when explicitly set it falls through to `throw new Error("cannot install vite dev server middleware for TanStack Start since the SSR environment is not a RunnableDevEnvironment")`. Nitro's env is a `createFetchableDevEnvironment` (`nitro/dist/_build/vite.env.mjs:32-39`), i.e. exactly the non-runnable case. True — but it is 4 lines × 3 files documenting an option nobody sets. |

### apps/examples/minimal-app

| # | Option | Class | Evidence |
| --- | --- | --- | --- |
| 12 | `server.port: 3010`, `copyPublicDir`, `routeFileIgnorePattern` | **A** | Same evidence as rows 1/6/9. |
| 13 | **Missing** `ssr.noExternal` while calling `setupRouterSsrQueryIntegration` | **Gap, not an option** | `apps/examples/minimal-app/src/router.tsx:4,38` imports and calls `setupRouterSsrQueryIntegration` with the facade's client. Built output has **two** react-query graphs: `_ssr/router-BbuEocWy.mjs` (Vite-bundled via the linked facade) and `_libs/react+tanstack__react-query.mjs` + `_libs/@tanstack/react-router-ssr-query+[...].mjs` (nitro-bundled). It doesn't crash only because nothing calls `useQuery` under SSR — and `src/sdk-wiring.test.ts:98` actively asserts `not.toMatch(/\buseQuery\s*\(/u)`. The example app is one `useQuery` away from the Wallow-ka3m failure. |
| 14 | **Missing** `use-sync-external-store` alias | **Currently benign** | Its server bundle contains **zero** `__require("react")` (different chunking; the shim is inlined and statically linked). Do not "fix by symmetry" without re-measuring. |

### packages/*/vite.config.ts (7 files, 341 lines total)

| # | Option | Class | Evidence |
| --- | --- | --- | --- |
| 15 | `build.rollupOptions` (vs Vite 8's `rolldownOptions`) | **A, cosmetic only** | `vite/dist/node/chunks/node.js:2736-2749`: `buildConfig.rolldownOptions ??= buildConfig.rollupOptions` plus a get/set proxy. The deprecation warning fires only for paths in `runtimeDeprecatedPath = new Set(["optimizeDeps", "ssr.optimizeDeps"])` (line 2729) — **`build.rollupOptions` is not deprecated**. Vite 8's lib-mode docs use `rolldownOptions`; renaming is optional. |
| 16 | `rollupOptions.external: (id) => !id.startsWith(".") && ...` + `isAbsoluteWindows` helper | **A** | Vite lib mode does not auto-externalize: "Make sure to also externalize any dependencies that you do not want to bundle into your library" (<https://vite.dev/guide/build#library-mode>). Duplicated **verbatim in all 7 files**. |
| 17 | `target/outDir/emptyOutDir/sourcemap/minify/formats/entryFileNames` | **A, but duplicated** | See §2 — 25 code lines are byte-identical across all 7 packages. |
| 18 | `packages/ui` `componentEntries()` (fs scan → 56 entries) | **A (both alternatives refuted)** | **Experiment U1**: removed `...componentEntries()`, rebuilt → `dist/components/button/index.js` is **not emitted** (only `button.js`, `button.styles.js`, and the tsc-emitted `index.d.ts`), so the `"./*": "./dist/components/*/index.js"` export breaks. `preserveModules` alone does not save a re-export-only barrel. **Experiment U2**: glob entry `lib.entry: [".../src/index.ts", ".../src/components/*/index.ts"]` → build fails, `[UNRESOLVED_ENTRY] Cannot resolve entry module src/components/*/index.ts` (rolldown 1.1.5). Globs are **not** supported. **Alternative exports map refuted too**: repointing `"./*"` at `dist/components/*/*.js` would drop the recipe exports — every barrel re-exports from *two* modules (`packages/ui/src/components/button/index.ts` exports from `./button` **and** `./button.styles`). |
| 19 | `packages/ui`/`packages/forms` `preserveModules` + `preserveModulesRoot` | **A** | Required for the `"./*"` subpath contract (ui, 56 dirs in `dist/components`) and for the drop-what-you-don't-import property in forms. Works under rolldown 1.1.5. |
| 20 | `packages/styles` `assetFileNames: "[name][extname]"` | **A** | Only package emitting assets; stable-name policy (Wallow-do5e). |

### packages/styles/src/vite.ts (`wallowStyles()`)

| # | Item | Class | Notes |
| --- | --- | --- | --- |
| 21 | `brandAssetsPlugin` returning `{ publicDir }` from `config()` | **A** | Composes with app config instead of a raw field; interacts with row 6 (nitro would otherwise drop the copy). |
| 22 | `forkThemePlugin` `enforce: "pre"` + `\0`-prefixed resolved id | **A** | Standard virtual-module convention; `pre` is needed so `@tailwindcss/vite` doesn't claim the `.css` id first. Not independently re-tested. |
| 23 | `wallowStyles()` returning `[forkThemePlugin, tailwindcss(), brandAssetsPlugin]` | **A** | Genuinely collapses three concerns into one call in all 3 apps. Nothing to simplify here — this file is the *model* for what the app configs should look like. |

**Bucket counts (25 rows):** **A = 20** · **B = 1** (row 4, `resolve.dedupe` — verified no-op) · **C = 2**
(rows 13 and 2b: real problems whose fix belongs in minimal-app / upstream rolldown, not in the two
big app configs) · **D = 2** (rows 10 and 22 — plausible and consistent with installed defaults, but
not exercised by an experiment).

---

## 2. Duplication, measured

**App configs.** `apps/wallow-web/vite.config.ts` (142 lines) and `apps/wallow-auth/vite.config.ts`
(157 lines) are the same config with three differences: wallow-auth adds `base`/`router.basepath`/
`nitro({ baseURL })`, and the two comment blocks are reworded (same claims, different examples). The
`resolve.alias` array (2 entries + 18 comment lines), the whole `ssr.noExternal` block (2 entries +
25 comment lines), `resolve.dedupe`, `resolve.tsconfigPaths`, `environments.client.build.copyPublicDir`,
`srcDirectory`, `importProtection`, `routeFileIgnorePattern` and the `installDevServerMiddleware`
paragraph are all present in both. **Roughly 95 of ~142 lines are comment.**

**Package configs.** 341 lines across 7 files. After stripping comments and blank lines, these
**25 lines are byte-identical in all seven**:

```ts
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
export default defineConfig({
  build: {
    target: "es2023", outDir: "dist", emptyOutDir: true, sourcemap: true, minify: false,
    lib: { entry: { index: fileURLToPath(new URL("src/index.ts", import.meta.url)) }, formats: ["es"] },
    rollupOptions: {
      external: (id) => !id.startsWith(".") && !id.startsWith("/") && !isAbsoluteWindows(id),
      output: { entryFileNames: "[name].js" },
    },
  },
});
function isAbsoluteWindows(id: string): boolean { return /^[a-zA-Z]:[\\/]/u.test(id); }
```

`packages/query/vite.config.ts` and `packages/auth/vite.config.ts` are **100 % identical** once
comments are stripped (verified by diff — empty). The only real variation across the seven:

| Package | Extra entries | `chunkFileNames` | `preserveModules` | `assetFileNames` |
| --- | --- | --- | --- | --- |
| auth | — | ✓ | — | — |
| query | — | ✓ | — | — |
| sdk | `server/index`, `server/passthrough`, `query/index` | ✓ | — | — |
| styles | `assets`, `vite` | ✓ | — | ✓ |
| testing | `render`, `sdk-harness`, `contrast`, `render-with-wallow` | ✓ | — | — |
| forms | — | — | ✓ | — |
| ui | 56 × `components/<name>/index` | — | ✓ | — |

---

## 3. Prioritized simplifications

### R1 — Collapse the 7 package configs into one `defineLibraryConfig()` (biggest win, zero risk)

341 lines → ~40 lines of shared helper + 7 files of 3–8 lines. Put the helper in
`packages/testing` (already a build-tooling package) or a new `tools/vite/library.ts`.

```ts
// tools/vite/library.ts
import { fileURLToPath } from "node:url";
import { defineConfig, type UserConfig } from "vite";

/** Every non-relative specifier stays external so a consumer never gets a second runtime copy. */
const externalizeBareSpecifiers = (id: string): boolean =>
  !id.startsWith(".") && !id.startsWith("/") && !/^[a-zA-Z]:[\\/]/u.test(id);

export function defineLibraryConfig(opts: {
  root: URL;                       // import.meta.url of the package's vite.config.ts
  entries: Record<string, string>; // key = emitted path (no extension), value = src path
  preserveModules?: boolean;       // ui + forms
  emitAssets?: boolean;            // styles
}): UserConfig {
  const resolve = (p: string): string => fileURLToPath(new URL(p, opts.root));
  return defineConfig({
    build: {
      target: "es2023", outDir: "dist", emptyOutDir: true, sourcemap: true, minify: false,
      lib: {
        entry: Object.fromEntries(Object.entries(opts.entries).map(([k, v]) => [k, resolve(v)])),
        formats: ["es"],
      },
      rollupOptions: {
        external: externalizeBareSpecifiers,
        output: {
          entryFileNames: "[name].js",
          ...(opts.preserveModules
            ? { preserveModules: true, preserveModulesRoot: "src" }
            : { chunkFileNames: "[name]-[hash].js" }),
          ...(opts.emitAssets ? { assetFileNames: "[name][extname]" } : {}),
        },
      },
    },
  });
}
```

Before / after for `packages/query/vite.config.ts` (41 lines → 5):

```ts
// before: 41 lines, 25 of them byte-identical to six other files
export default defineConfig({ build: { target: "es2023", outDir: "dist", /* ...20 more lines... */ } });

// after
import { defineLibraryConfig } from "../../tools/vite/library";
// Externalizing @tanstack/react-query is the whole point of this facade: a bundled copy
// would hand consumers a second QueryClientProvider context.
export default defineLibraryConfig({ root: import.meta.url, entries: { index: "src/index.ts" } });
```

and `packages/ui` (67 → ~14, keeping the fs scan verified necessary in row 18):

```ts
import { globSync } from "node:fs";              // Node 24 — replaces readdirSync + existsSync
import { defineLibraryConfig } from "../../tools/vite/library";

// `"./*"` in package.json points at dist/components/<name>/index.js, and a re-export-only
// barrel is inlined into its importer unless it is a named entry (verified: dropping these
// emits no index.js). Rolldown 1.1.5 rejects glob entries, so the map is built here.
const componentEntries = Object.fromEntries(
  globSync("components/*/index.ts", { cwd: new URL("src/", import.meta.url) })
    .map((p) => [p.replace(/\.ts$/u, ""), `src/${p}`]),
);

export default defineLibraryConfig({
  root: import.meta.url,
  entries: { index: "src/index.ts", ...componentEntries },
  preserveModules: true,
});
```

Caveat: a helper outside the package directory must be reachable from each package's
`tsconfig`/lint config. If that turns out to be friction, publish it from
`@bc-solutions-coder/testing` (already a devDependency of every package) instead of a bare
relative path.

### R2 — Deduplicate the two app configs' shared block

`ssr.noExternal`, the `use-sync-external-store` alias, `resolve.tsconfigPaths`,
`environments.client.build.copyPublicDir`, `importProtection` and `routeFileIgnorePattern` are
identical in wallow-web and wallow-auth, and three of them belong in minimal-app too (row 13).
Ship them as a plugin from `packages/styles/src/vite.ts`'s sibling — i.e. a `wallowApp()` preset
that returns a partial `UserConfig` from a `config()` hook, exactly the pattern
`brandAssetsPlugin` already uses:

```ts
// packages/styles/src/vite.ts (or a new packages/vite-preset)
export const wallowAppDefaults: Plugin = {
  name: "wallow:app-defaults",
  config(): UserConfig {
    return {
      // Base UI's useIsHydrating imports the CJS shim; rolldown leaves its bare require()
      // as a runtime createRequire, loading a SECOND React. Verified: without this,
      // wallow-auth /login SSR throws "Invalid hook call" and ships an empty shell.
      resolve: {
        alias: [
          { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
          { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
        ],
        tsconfigPaths: true,
      },
      // Vite externalizes deps for SSR except LINKED ones. The query facade is linked
      // (bundled) while react-router-ssr-query is not (nitro bundles its own react-query),
      // giving two QueryClient contexts. Verified: without this, /login logs
      // "No QueryClient set" and renders 2621 chars instead of 9895.
      ssr: { noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"] },
      // nitro/dist/vite.mjs:288 does `config.build.copyPublicDir ??= false`; setting it
      // here wins, so the shared brand assets still land in .output/public.
      environments: { client: { build: { copyPublicDir: true } } },
    };
  },
};
```

Each app config then drops to ~35 lines (port, plugins, and — for wallow-auth — the base-path
trio). **This also closes row 13**: minimal-app picks up the react-query fix for free. Keep
`resolve.dedupe` out of the preset (row 4: verified no-op).

Alternative worth considering for the react-query half: `ssr.external:
["@bc-solutions-coder/query"]` produced a byte-identical render with **one** react-query graph
(experiment A3) and needs no list of future consumers — but it externalizes a linked package, so
the facade loses dev HMR. Recommendation: keep `noExternal` (current behaviour, HMR preserved)
and treat A3 as the documented fallback.

### R3 — Cut the comments by ~70 %, keep the claims

The two app configs are ~95/142 lines of prose. Every claim in them is *true* (that is the
finding), but each can be one or two lines now that the evidence is written down here. Suggested
survivors, one line each: (a) the shim alias exists because Base UI's `useIsHydrating` + rolldown
CJS interop ⇒ second React; (b) `ssr.noExternal` exists because linked-vs-external splits
react-query; (c) `copyPublicDir` exists because `nitro/vite.mjs:288` uses `??=`; (d)
`importProtection.include` must accompany a narrowed `srcDirectory`
(`adapterUtils.js:22-24`). Delete the `installDevServerMiddleware` paragraph from all three apps
(row 11) — it documents an option nobody sets, and the plugin's auto-detect handles it.

### R4 — Two beads to file (real problems this review surfaced)

1. **`use-sync-external-store/shim/with-selector` still runtime-`__require`s react** in the shipped
   server bundle of both apps (row 2b), and `useSyncExternalStoreWithSelector` is called from three
   live modules. The alias cannot cover it (React ships no `useSyncExternalStoreWithSelector`).
   Needs either a rolldown-level fix for bare `require()` inside `__commonJSMin` wrappers, or a
   `resolve.alias` pointing the subpath at a local ESM re-implementation.
2. **`apps/examples/minimal-app` has the duplicate-QueryClient split unguarded** (row 13). R2 fixes
   it; until then the example ships the bug the two real apps were patched for.

### R5 — Non-changes (explicitly rejected)

- Do **not** delete the `use-sync-external-store` alias. It is live, and removing it breaks
  wallow-auth SSR (reproduced).
- Do **not** replace `componentEntries()` with a glob — rolldown 1.1.5 rejects glob entries.
- Do **not** replace the per-component entries with an exports-map change — the barrels re-export
  from two modules each.
- Do **not** rename `build.rollupOptions` → `rolldownOptions` expecting a warning to go away;
  `build.rollupOptions` is not on Vite 8's deprecation list.

---

## 4. What I could not verify

- **Row 10** (wallow-auth's `base` / `router.basepath` / `nitro baseURL` trio): I confirmed nitro's
  default is `baseURL: "/"` but did not run a prefixed build (`WALLOW_AUTH_BASE_PATH=/auth pnpm build`)
  and curl the prefixed assets. The claim is consistent with the installed defaults; treat as
  probable, not proven.
- **Row 22** (`forkThemePlugin` `enforce: "pre"` ordering vs `@tailwindcss/vite`): not tested by
  removing `enforce` and observing a failure.
- **Row 1** (`server.port`): not re-tested; the reasoning is in `.claude/rules/E2E.md` and the cost
  of keeping it is one line.
- **Row 11**: I verified the plugin's auto-detect branch by reading it, but did not run
  `vite dev` with `installDevServerMiddleware: true` to observe the thrown error.
- I did not measure whether R1/R2 change build **output** byte-for-byte. Both are pure config
  refactors, but the refactor PR should diff `.output/` before and after.
