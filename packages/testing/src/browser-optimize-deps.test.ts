import { describe, expect, it } from "vitest";

import { browserOptimizeDepsBaseline, mergeOptimizeDeps } from "./browser-optimize-deps";

// Unit guard for the `mergeOptimizeDeps` helper that every consuming app's
// browser Vitest project layers its extras on with.
//
// The baseline's CONTENTS are deliberately not asserted here. Pinning the list
// froze the shared `optimizeDeps.include` — every change to it, including
// removing entries that resolve to nothing, had to edit this file first. What
// actually matters about an entry is that it RESOLVES; that is verified against
// the real dep graph, not against a copy of the list.

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
