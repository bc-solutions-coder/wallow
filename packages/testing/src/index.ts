// Barrel entry for @bc-solutions-coder/testing: the shared Vitest preset factory
// and the browser-mode optimizeDeps baseline + merge helper.
//
// This barrel is CONFIG-SAFE: it is imported at Vitest config-load time (in a
// plain Node process) by each app's vitest.config.ts, so it must NOT transitively
// pull in browser-only modules. `render` re-exports `vitest-browser-react`, which
// evaluates `vitest/browser` at import and THROWS outside browser mode — it is
// therefore exposed on the dedicated `@bc-solutions-coder/testing/render` subpath
// (see package.json exports), never from this barrel.
export { browserOptimizeDepsBaseline, mergeOptimizeDeps } from "./browser-optimize-deps";
export {
  createVitestProjects,
  type VitestBrowserConfig,
  type VitestBrowserInstance,
  type VitestBrowserProject,
  type VitestBrowserTestConfig,
  type VitestNodeProject,
  type VitestNodeTestConfig,
  type VitestProjectsOptions,
  type VitestProjectsPair,
} from "./vitest-projects";
