import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-web. See apps/wallow-auth/vitest.config.ts for the
 * shared shape — the node/browser split, the `*.ssr.test.tsx` convention that
 * routes render-nothing specs onto node, and why the styling pair is required
 * rather than cosmetic. wallow-web adds three things:
 *
 *   - `react-dom/server` in the pre-bundle list, because
 *     `DashboardLayout.ssr-flash.test.tsx` renders the shell the way the BFF
 *     does — through `react-dom/server` — INSIDE Chromium, so the pre-hydration
 *     paint can be measured against real CSS at a real viewport.
 *   - the `node:async_hooks` alias below.
 *   - `ssr.noExternal` for the linked workspace packages.
 *
 * Deliberately absent: `@base-ui/react/*` and `@tanstack/react-query`. This app
 * declares neither package — it reaches Base UI through
 * `@bc-solutions-coder/ui` and react-query through the
 * `@bc-solutions-coder/query` facade — and under pnpm's strict `node_modules` a
 * package resolves only what it declares, so those eight entries logged one
 * `Failed to resolve dependency` warning each and pre-bundled nothing. The fix
 * is not to declare them: naming Base UI here would give the app a second route
 * to a package the catalog exists to own, and the facade entry is what actually
 * pre-bundles react-query's runtime. `src/browser-deps.test.ts` fails now if an
 * entry stops resolving, so this cannot silently come back.
 */

// `src/app/router.tsx` imports `@tanstack/react-start`, which loads Start's
// per-request `AsyncLocalStorage` from `node:async_hooks` at module scope. A real
// client build never sees that module (the Start plugin compiles the isomorphic
// helper down to its client branch), but the browser project runs without that
// plugin and vitest externalises `node:async_hooks` to a throwing proxy, so every
// spec importing the router died at import. Point it at a real in-browser
// implementation instead; see the shim for why answering "no scope" is correct.
const nodeAsyncHooksShim = "@bc-solutions-coder/testing/node-async-hooks-browser-shim";

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [
    "react",
    "react/jsx-dev-runtime",
    "react/jsx-runtime",
    "react-dom",
    "react-dom/server",
    // The query facade and the auth package that rides on it are LINKED
    // workspace packages, and Vite does not pre-bundle a link by default. Named
    // here, the pre-bundled chunk carries react-query's runtime inlined, so the
    // browser graph holds exactly ONE react-query copy (and therefore one
    // `QueryClientProvider` context — two would surface as "No QueryClient set"
    // from a provider the hook does not recognise).
    "@bc-solutions-coder/query",
    "@bc-solutions-coder/auth",
    "@tanstack/react-router",
    "@tanstack/react-form",
    "zustand",
    "lucide-react",
    // Arrives with @bc-solutions-coder/forms: a migrated form imports it for its
    // schema, and it is the schema module — not the form package — that the
    // scanner misses on the first pass.
    "zod",
  ],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  // Stated once at the root and pulled in by each project's `extends: true`; see
  // wallow-auth's config for why a project inherits nothing without it.
  resolve: { tsconfigPaths: true },
  // The other half of the linked-facade wiring, for the NODE project: without
  // `ssr.noExternal` Vite externalizes the linked package to a bare Node import
  // instead of transforming its source, so the SSR-side route specs never see it.
  // Same knob `packages/testing`'s own config carries.
  ssr: {
    noExternal: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth"],
  },
  test: {
    projects: [
      { ...node, extends: true },
      {
        ...browser,
        extends: true,
        // `node:async_hooks` stays in `alias` — `tsconfig.json` `paths` cannot
        // express it, and without it every spec importing `src/app/router.tsx`
        // dies at import. `tsconfigPaths` is NOT restated: a project-level
        // `resolve` MERGES into the inherited one rather than replacing it, so
        // the root's setting survives this. Verified by running an alias-heavy
        // browser spec (`routes/bff-demo.test.tsx`, 10 tests) with only the
        // alias here — green, and red only if the root's `resolve` is removed.
        resolve: { alias: { "node:async_hooks": nodeAsyncHooksShim } },
      },
    ],
  },
});
