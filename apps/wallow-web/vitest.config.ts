import { fileURLToPath } from "node:url";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-web — the shared node + real-Chromium two-project
 * split from `@bc-solutions-coder/testing`'s `createVitestProjects` preset,
 * configured with wallow-web's app-local knobs.
 *
 * Two runtimes, one per project (owned by the preset):
 *
 *   node    — every pure-logic spec (`src/shared/lib/*.test.ts`, the BFF host wiring)
 *             PLUS the SSR route specs that render via `react-dom/server`
 *             (`renderToString`) or assert a route's `beforeLoad` redirect and
 *             never mount a live DOM, listed in `nodeTsxSpecs`.
 *   browser — every component spec (`*.test.tsx` that mounts a component) plus
 *             the browser-native smoke spec, in headless Chromium via the
 *             preset's `playwright()` provider with ZERO jsdom involvement.
 *
 * The `openid-client` alias and inlined workspace SDK this config used to carry
 * are gone with the old `bff-server.test.ts`: the BFF wiring is the SDK's now,
 * and the host spec that replaced it (`src/app/lib/bff.server.test.ts`) mocks the SDK's
 * server entry directly, so nothing needs the transitive `openid-client` import
 * to resolve to one shared module id.
 */
const nodeTsxSpecs: string[] = [
  "src/app/routes/index.test.tsx",
  "src/app/routes/dashboard/route.test.tsx",
  // `useIsDesktop`'s SERVER snapshot — the one branch a browser-mode spec cannot
  // reach, since a mount always takes the client snapshot. Renders a probe
  // through `react-dom/server` and mounts nothing.
  "src/shared/lib/use-is-desktop.ssr.test.tsx",
];

// Browser render/runtime libraries wallow-web pulls in beyond the shared preset
// baseline, pre-bundled so the browser provider does not discover them mid-run
// and trigger a Vite reload ("Vitest unexpectedly reloaded a test"), which flakes.
const extraBrowserOptimizeDeps: string[] = [
  "react",
  "react/jsx-dev-runtime",
  "react/jsx-runtime",
  "react-dom",
  // `DashboardLayout.ssr-flash.test.tsx` renders the shell the way the BFF does
  // — through `react-dom/server` — inside Chromium, so the pre-hydration paint
  // can be measured against real CSS at a real viewport. Discovered mid-run it
  // triggers a Vite reload ("Vitest unexpectedly reloaded a test").
  "react-dom/server",
  // The query facade and the auth package that rides on it are LINKED workspace
  // packages, and Vite does not pre-bundle a link by default. Named here, the
  // pre-bundled chunk carries react-query's runtime inlined, so the browser graph
  // holds exactly ONE react-query copy (and therefore one `QueryClientProvider`
  // context — two would surface as "No QueryClient set" from a provider the hook
  // does not recognise).
  "@bc-solutions-coder/query",
  "@bc-solutions-coder/auth",
  // Stays on the list as policy: react-query is still the module actually being
  // pre-bundled, one facade hop away. It no longer resolves from this app's root
  // (the manifest dropped it), so Vite logs one "Failed to resolve dependency"
  // line per run; the pre-bundling it names is done by the facade entry above.
  "@tanstack/react-query",
  "@tanstack/react-router",
  "@tanstack/react-form",
  "zustand",
  "lucide-react",
  // Arrives with @bc-solutions-coder/forms: a migrated form imports it for its
  // schema, and it is the schema module — not the form package — that the
  // scanner misses on the first pass. Every form this app migrates needs it.
  "zod",
  // The catalog components wallow-web mounts reach Base UI through per-component
  // SUBPATHS, and Vite pre-bundles a subpath only when it is named — the package
  // root does not cover them.
  "@base-ui/react/autocomplete",
  "@base-ui/react/checkbox",
  // The add-member picker is an `Autocomplete`, whose parts are Base UI's
  // COMBOBOX parts re-exported — the autocomplete subpath alone does not cover
  // them.
  "@base-ui/react/combobox",
  "@base-ui/react/navigation-menu",
  "@base-ui/react/select",
  "@base-ui/react/toggle",
  "@base-ui/react/toggle-group",
];

// `src/app/router.tsx` imports `@tanstack/react-start`, which loads Start's
// per-request `AsyncLocalStorage` from `node:async_hooks` at module scope. A real
// client build never sees that module (the Start plugin compiles the isomorphic
// helper down to its client branch), but the browser project runs without that
// plugin and vitest externalises `node:async_hooks` to a throwing proxy, so every
// spec importing the router died at import. Point it at a real in-browser
// implementation instead; see the shim for why answering "no scope" is correct.
const nodeAsyncHooksShim: string = fileURLToPath(
  new URL("src/shared/testing/node-async-hooks-browser-shim.ts", import.meta.url),
);

const { node, browser } = createVitestProjects({ nodeTsxSpecs, extraBrowserOptimizeDeps });

export default defineConfig({
  // The other half of the linked-facade wiring, for the NODE project: without
  // `ssr.noExternal` Vite externalizes the linked package to a bare Node import
  // instead of transforming its source, so the SSR-side route specs never see it.
  // Same knob `packages/testing`'s own config carries.
  ssr: {
    noExternal: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth"],
  },
  test: {
    // `resolve` is PER PROJECT — a root-level `resolve` is NOT inherited by
    // `test.projects`, so `tsconfigPaths` has to be repeated in each entry. Both
    // read the same `tsconfig.json` `paths` the app builds against, which is why
    // the runtime and the test runner cannot disagree about a zone.
    projects: [
      { ...node, resolve: { tsconfigPaths: true } },
      {
        ...browser,
        // The styling the preset deliberately leaves to each consumer, wired the
        // same way packages/forms and packages/ui do it: `wallowStyles()` (the
        // `@tailwindcss/vite` + brand-assets pair) compiles ./vitest-styles.css
        // and ./vitest.setup.ts loads it plus the fork theme into the page.
        //
        // Not cosmetic. A ui control gets its BOX from a Tailwind utility in its
        // recipe, so with no stylesheet the catalog checkbox's
        // `<span role="checkbox">` measures 0x0 and every spec that CLICKS it
        // hangs to the actionability timeout.
        plugins: wallowStyles(),
        // `node:async_hooks` stays in `alias` — `tsconfig.json` `paths` cannot
        // express it, and without it every spec importing `src/app/router.tsx`
        // dies at import.
        resolve: { tsconfigPaths: true, alias: { "node:async_hooks": nodeAsyncHooksShim } },
        test: { ...browser.test, setupFiles: ["./vitest.setup.ts"] },
      },
    ],
  },
});
