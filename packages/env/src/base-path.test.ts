import { describe, expect, it, vi } from "vitest";

import { normalizeBasePath, stripBasePath, toViteBase, withBasePath } from "./base-path";

/**
 * The string arithmetic behind serving an app under a URL prefix.
 *
 * The segment-boundary rule in {@link stripBasePath} is the case that bites: a
 * passthrough forwards whatever pathname survives this function straight to the
 * API, so a bare `startsWith` here would let `/authentic/v1/...` through mangled.
 */

describe("normalizeBasePath", () => {
  it.each([
    ["undefined (the default — no prefix)", undefined, ""],
    ["the empty string", "", ""],
    ["a bare slash", "/", ""],
    ["repeated slashes that name the root", "///", ""],
    ["a prefix written without its leading slash", "auth", "/auth"],
    ["the canonical form", "/auth", "/auth"],
    ["a trailing slash", "/auth/", "/auth"],
    ["slashes on both ends", "//auth//", "/auth"],
    ["a multi-segment prefix", "/apps/auth", "/apps/auth"],
    ["surrounding whitespace from a hand-edited env file", "  /auth  ", "/auth"],
  ])("normalizes %s", (_label: string, input: string | undefined, expected: string) => {
    expect(normalizeBasePath(input)).toBe(expected);
  });

  it("is idempotent, so a value that has already been through it is unchanged", () => {
    expect(normalizeBasePath(normalizeBasePath("auth/"))).toBe("/auth");
  });
});

describe("toViteBase", () => {
  it("spells no prefix the way Vite's `base` does — a bare slash", () => {
    expect(toViteBase("")).toBe("/");
  });

  it("adds the trailing slash Vite needs to join asset paths onto the base", () => {
    expect(toViteBase("/auth")).toBe("/auth/");
  });

  it("handles a multi-segment prefix", () => {
    expect(toViteBase("/apps/auth")).toBe("/apps/auth/");
  });
});

describe("stripBasePath", () => {
  it("returns the pathname untouched when no base path is configured", () => {
    expect(stripBasePath("/v1/me", "")).toBe("/v1/me");
  });

  it("removes the prefix from a path below it", () => {
    expect(stripBasePath("/auth/v1/me", "/auth")).toBe("/v1/me");
  });

  it("removes a multi-segment prefix", () => {
    expect(stripBasePath("/apps/auth/connect/token", "/apps/auth")).toBe("/connect/token");
  });

  it("rebases the base path itself to the root rather than to the empty string", () => {
    // `new URL("", base)` and a bare-string fetch both mis-resolve an empty
    // pathname, so the root must be spelled out.
    expect(stripBasePath("/auth", "/auth")).toBe("/");
  });

  it("rebases the base path with its trailing slash to the root", () => {
    expect(stripBasePath("/auth/", "/auth")).toBe("/");
  });

  it("leaves a path that never carried the prefix alone", () => {
    // Reachable in practice: a router's basepath rewrite is a no-op for a path
    // that does not start with the prefix, so `/v1/me` still matches `/v1/$`.
    expect(stripBasePath("/v1/me", "/auth")).toBe("/v1/me");
  });

  it("does not strip on a partial-segment match", () => {
    expect(stripBasePath("/authentic/v1/me", "/auth")).toBe("/authentic/v1/me");
  });

  it("strips only the leading occurrence, not a repeat further down the path", () => {
    expect(stripBasePath("/auth/v1/auth/me", "/auth")).toBe("/v1/auth/me");
  });
});

describe("withBasePath", () => {
  it("returns the origin unchanged when no base path is configured", () => {
    expect(withBasePath("https://wallow.dev", "")).toBe("https://wallow.dev");
  });

  it("appends the base path, so the SDK calls the prefix a based app answers on", () => {
    expect(withBasePath("https://wallow.dev", "/auth")).toBe("https://wallow.dev/auth");
  });

  it("does not leave a trailing slash for the SDK to double up on", () => {
    expect(withBasePath("http://localhost:3002", "/auth")).not.toMatch(/\/$/u);
  });
});

describe("importing this module has no environmental preconditions", () => {
  it("evaluates from scratch with the process environment emptied", async () => {
    // `apps/wallow-auth/vite.config.ts` imports this at CONFIG LOAD time, as
    // plain Node ESM before any bundle or runtime env exists. A module that read
    // or validated an env var here would make `vite build` fail on a missing
    // RUNTIME variable — the opposite of what a boot-time check is for.
    const saved: NodeJS.ProcessEnv = process.env;
    process.env = {};
    vi.resetModules();

    try {
      const module = await import("./base-path");

      expect(Object.keys(module).toSorted()).toEqual([
        "normalizeBasePath",
        "stripBasePath",
        "toViteBase",
        "withBasePath",
      ]);
    } finally {
      process.env = saved;
    }
  });
});
