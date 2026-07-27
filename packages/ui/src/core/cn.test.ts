import { describe, expect, it } from "vitest";

import { cn } from "./cn";

// Acceptance-criteria guard for Wallow-m5aq.1.2 (core/cn.ts, the recipe
// class-merge helper). `cn` is layer 0 of the new package layering: it imports
// nothing from ../components, so this spec is pure logic and runs in the node
// vitest project (.test.ts, not .test.tsx).
//
// The helper exists so every component part can run `recipe + caller className`
// through one place and have the caller reliably win — the last two specs pin
// that contract, the first three pin the primitive join/filter/conflict rules.

describe("cn", () => {
  it("joins class values", () => {
    expect(cn("a", "b")).toBe("a b");
  });

  it("drops falsy values", () => {
    expect(cn("a", undefined, false, "b")).toBe("a b");
  });

  it("resolves tailwind conflicts, last wins", () => {
    expect(cn("px-3 bg-primary", "bg-secondary")).toBe("px-3 bg-secondary");
  });

  it("returns an empty string when nothing usable is passed", () => {
    expect(cn()).toBe("");
    expect(cn(undefined, false, null)).toBe("");
  });

  it("lets a caller className override a single recipe utility", () => {
    // The real reason this helper exists: a component part merges its recipe
    // with the caller's className and the caller overrides only what it names,
    // keeping every untouched utility from the recipe.
    const recipe =
      "w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground";

    expect(cn(recipe, "bg-destructive")).toBe(
      "w-full rounded-md px-3 py-2 text-sm font-medium text-primary-foreground bg-destructive",
    );
  });
});
