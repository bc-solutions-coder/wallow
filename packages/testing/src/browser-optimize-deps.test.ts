import { describe, expect, it } from "vitest";

import { browserOptimizeDepsBaseline, mergeOptimizeDeps } from "./browser-optimize-deps";

// Unit guard for Wallow-0q2s.1.2: the shared browser-mode `optimizeDeps.include`
// baseline + merge helper that every consuming app's browser Vitest project
// pre-bundles. The baseline is the list named on the bead design (the union both
// apps need pre-bundled: vitest-browser-react + the react jsx runtimes +
// react-dom/client); apps layer their extras on via `mergeOptimizeDeps`.

// The render helpers Vitest must pre-bundle for EVERY browser project, or the
// provider re-optimizes them mid-run and drops the runner.
const REQUIRED_BASELINE = [
  "vitest-browser-react",
  "react/jsx-runtime",
  "react/jsx-dev-runtime",
  "react-dom/client",
] as const;

describe("browserOptimizeDepsBaseline", () => {
  it("includes every required browser-render dependency", () => {
    for (const dep of REQUIRED_BASELINE) {
      expect(browserOptimizeDepsBaseline).toContain(dep);
    }
  });

  it("leads with the vitest-browser-react render helper", () => {
    expect(browserOptimizeDepsBaseline[0]).toBe("vitest-browser-react");
  });

  it("carries no duplicate entries", () => {
    expect(new Set(browserOptimizeDepsBaseline).size).toBe(browserOptimizeDepsBaseline.length);
  });
});

describe("mergeOptimizeDeps", () => {
  it("returns exactly the baseline when there are no extras", () => {
    expect(mergeOptimizeDeps([])).toEqual([...browserOptimizeDepsBaseline]);
  });

  it("appends app-specific extras after the baseline", () => {
    const extras = ["@tanstack/react-query", "@tanstack/react-router"];
    const merged = mergeOptimizeDeps(extras);

    // Baseline preserved, in order, at the front.
    expect(merged.slice(0, browserOptimizeDepsBaseline.length)).toEqual([
      ...browserOptimizeDepsBaseline,
    ]);
    // Extras present.
    for (const extra of extras) {
      expect(merged).toContain(extra);
    }
  });

  it("de-duplicates extras that already appear in the baseline", () => {
    const merged = mergeOptimizeDeps(["react/jsx-runtime", "@tanstack/react-form"]);

    expect(merged.filter((entry) => entry === "react/jsx-runtime")).toHaveLength(1);
    expect(merged).toContain("@tanstack/react-form");
  });

  it("does not mutate the shared baseline array", () => {
    const before = [...browserOptimizeDepsBaseline];
    mergeOptimizeDeps(["react", "react-dom"]);
    expect([...browserOptimizeDepsBaseline]).toEqual(before);
  });
});
