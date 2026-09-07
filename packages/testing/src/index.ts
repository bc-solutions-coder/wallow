// Node-safe Vitest configuration helpers. Browser render helpers have separate subpaths.
export { browserOptimizeDepsBaseline, mergeOptimizeDeps } from "./browser-optimize-deps";
export {
  createVitestProjects,
  ssrSpecGlob,
  type VitestBrowserConfig,
  type VitestBrowserInstance,
  type VitestBrowserProject,
  type VitestBrowserTestConfig,
  type VitestNodeProject,
  type VitestNodeTestConfig,
  type VitestProjectsOptions,
  type VitestProjectsPair,
} from "./vitest-projects";
