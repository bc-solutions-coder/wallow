import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth — the shared node + headless-Chromium split
 * from `@bc-solutions-coder/testing`'s `createVitestProjects`, plus this app's
 * two knobs.
 *
 * `*.ssr.test.tsx` is the node-project convention: a spec that renders through
 * `react-dom/server` or asserts a route's `beforeLoad` redirect never mounts a
 * live DOM, so routing it into a browser buys nothing and costs real per-test
 * overhead. Every OTHER `*.test.tsx` mounts a component and runs in Chromium.
 * The preset owns the convention; this file no longer lists the files.
 *
 * `wallowStyles()` + ./vitest.setup.ts are not cosmetic on either half
 * (Wallow-8ytl — before them this app's browser project loaded NO stylesheet).
 * The UTILITIES give a ui control its box: without them the catalog checkbox's
 * `<span role="checkbox">` measures 0x0 and a click hangs to Playwright's
 * actionability timeout. The THEME gives the colour utilities their values:
 * without it every colour computes to transparent, so a rendered-colour
 * assertion could not tell a contrast defect from a passing test.
 *
 * The pre-bundle entries name TanStack Query under its facade,
 * `@bc-solutions-coder/query`, and never under the react-query specifier the
 * facade re-exports — this app does not declare react-query, so under pnpm's
 * strict `node_modules` it cannot resolve that specifier at all, and an
 * unresolvable entry is only a WARNING after which Vite pre-bundles nothing.
 * `@bc-solutions-coder/auth` rides on that facade and is a LINKED workspace
 * package too, so it needs naming for the same reason (`routes/invitation.tsx`
 * reads the visitor through its `useCurrentUser`). `zod` arrives with
 * @bc-solutions-coder/forms: a migrated screen imports it for its schema, and it
 * is the schema module — not the form package — that the scanner misses.
 */
// `routes/__root.tsx` imports `@tanstack/react-start`, which loads Start's
// per-request `AsyncLocalStorage` from `node:async_hooks` at module scope. A real
// client build never sees that module (the Start plugin compiles the isomorphic
// helper down to its client branch), but the browser project runs without that
// plugin and vitest externalises `node:async_hooks` to a throwing proxy, so every
// spec importing the root route died at import. Point it at a real in-browser
// implementation instead; see the shim for why answering "no scope" is correct.
const nodeAsyncHooksShim = "@bc-solutions-coder/testing/node-async-hooks-browser-shim";

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: ["@bc-solutions-coder/query", "@bc-solutions-coder/auth", "zod"],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  // Stated ONCE, at the root, and pulled into each project by `extends: true`.
  // A project inherits nothing from the root file by default — that is what the
  // old "`resolve` is PER PROJECT" comment recorded — but `extends` makes the
  // root config a real base rather than dead weight. Both projects read the same
  // `tsconfig.json` `paths` the app builds against, which is why the runtime and
  // the test runner cannot disagree about a zone.
  resolve: { tsconfigPaths: true },
  test: {
    projects: [
      { ...node, extends: true },
      {
        ...browser,
        extends: true,
        // `node:async_hooks` stays in `alias` — `tsconfig.json` `paths` cannot
        // express it. `tsconfigPaths` is NOT restated: a project-level `resolve`
        // MERGES into the inherited one rather than replacing it, so the root's
        // setting survives this (see wallow-web's config for the verification).
        resolve: { alias: { "node:async_hooks": nodeAsyncHooksShim } },
      },
    ],
  },
});
