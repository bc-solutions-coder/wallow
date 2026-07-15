import { describe, expect, it } from "vitest";

import * as generated from "./generated";

describe("generated SDK", () => {
  it("re-exports at least one callable SDK function", () => {
    const functions: unknown[] = Object.values(generated).filter(
      (value: unknown): value is (...args: unknown[]) => unknown => typeof value === "function",
    );

    expect(functions.length).toBeGreaterThan(0);
  });

  it("exposes generated members", () => {
    expect(Object.keys(generated).length).toBeGreaterThan(0);
  });
});
