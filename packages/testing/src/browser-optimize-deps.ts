/**
 * Baseline `optimizeDeps.include` list every browser (Chromium) Vitest project
 * needs pre-bundled so the browser provider does not discover the render helpers
 * mid-run and trigger a Vite reload ("Vitest failed to find the runner").
 *
 * `vitest-browser-react` leads because it is the render seam every component
 * spec imports; the react JSX runtimes + `react-dom/client` back it. Apps layer
 * their own extras (e.g. the `@tanstack/*` packages wallow-web renders) on top
 * via {@link mergeOptimizeDeps}.
 */
export const browserOptimizeDepsBaseline: readonly string[] = [
  "vitest-browser-react",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  "react-dom/client",
];

/**
 * Merge app-specific extras onto the shared baseline: the baseline comes first,
 * in order, then any extra not already present is appended. Never mutates the
 * shared baseline array and never emits a duplicate.
 */
export function mergeOptimizeDeps(extra: readonly string[]): string[] {
  const merged: string[] = [...browserOptimizeDepsBaseline];
  for (const dep of extra) {
    if (!merged.includes(dep)) {
      merged.push(dep);
    }
  }
  return merged;
}
