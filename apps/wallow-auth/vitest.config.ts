import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth — the shared two-project (node + headless
 * Chromium) split now lives in `@bc-solutions-coder/testing`'s
 * `createVitestProjects` preset (Wallow-0q2s.1.3). This config only supplies the
 * app-specific knobs.
 *
 * The pure-logic/SSR `*.test.tsx` specs listed below render through
 * `react-dom/server` (or assert a route's `beforeLoad` redirect) and never mount
 * a live DOM, so they run on the node project rather than in Chromium — routing
 * them into a browser buys nothing and costs real per-test overhead. Every OTHER
 * `*.test.tsx` mounts a component and belongs in the browser project.
 * wallow-auth needs no node-project overrides.
 */
const nodeTsxSpecs = ["src/app/routes/index.test.tsx"];

/*
 * Runtimes wallow-auth reaches beyond the preset baseline, pre-bundled so the
 * browser provider does not discover them mid-run and trigger a Vite reload
 * ("Vitest unexpectedly reloaded a test"), which flakes the whole file.
 *
 * `zod` arrives with @bc-solutions-coder/forms: a migrated screen imports it for
 * its schema, and it is the schema module — not the form package — that the
 * scanner misses on the first pass. Every form this app migrates needs it.
 *
 * TanStack Query appears here under its facade name, @bc-solutions-coder/query,
 * and never under the react-query specifier the facade re-exports: this app no
 * longer declares react-query, so under pnpm's strict `node_modules` it cannot
 * resolve that specifier at all — and an unresolvable `optimizeDeps` entry is
 * only a WARNING, after which Vite pre-bundles nothing and the discovery reload
 * comes back with a config that looks correct. packages/forms hit this first.
 *
 * @bc-solutions-coder/auth rides on that facade and is a LINKED workspace package
 * too, so it needs naming for the same reason: `routes/invitation.tsx` reads the
 * visitor through its `useCurrentUser` (Wallow-x4qn.9.2), and wallow-web's config
 * already names it for this exact reason.
 */
const extraBrowserOptimizeDeps: string[] = [
  "@bc-solutions-coder/query",
  "@bc-solutions-coder/auth",
  "zod",
];

const { node, browser } = createVitestProjects({ nodeTsxSpecs, extraBrowserOptimizeDeps });

export default defineConfig({
  test: {
    // `resolve` is PER PROJECT — a root-level `resolve` is NOT inherited by
    // `test.projects`, so `tsconfigPaths` has to be repeated in each entry. Both
    // read the same `tsconfig.json` `paths` the app builds against; vitest
    // resolves specifiers itself, so an option missing here fails only under test
    // even though the app builds.
    projects: [
      { ...node, resolve: { tsconfigPaths: true } },
      { ...browser, resolve: { tsconfigPaths: true } },
    ],
  },
});
