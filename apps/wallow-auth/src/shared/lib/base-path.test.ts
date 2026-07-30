import { describe, expect, it } from "vitest";

import {
  AUTH_BASE_PATH_ENV_KEY,
  BASE_PATH,
  normalizeBasePath,
  stripBasePath,
  toViteBase,
  withBasePath,
} from "./base-path";

/**
 * The string arithmetic behind `AUTH_BASE_PATH` (Wallow-vufu.2.2).
 *
 * Node project: pure string work, no DOM.
 *
 * Every case below came out of the spike that built this app with
 * `AUTH_BASE_PATH=/auth` and drove the built Nitro server. The two that look
 * pedantic are the two that actually bite:
 *
 *  - the segment-boundary rule in {@link stripBasePath}, because the passthrough
 *    forwards whatever pathname survives this function straight to the API, so a
 *    `startsWith` here would let `/authentic/v1/...` through mangled; and
 *  - the empty-base identity cases, because "unchanged with AUTH_BASE_PATH unset"
 *    is half of this task's acceptance criteria, not a nicety.
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
    // Reachable in practice: the router's basepath rewrite is a no-op for a path
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

  it("appends the base path, so the SDK calls the prefix this app answers on", () => {
    expect(withBasePath("https://wallow.dev", "/auth")).toBe("https://wallow.dev/auth");
  });

  it("does not leave a trailing slash for the SDK to double up on", () => {
    expect(withBasePath("http://localhost:3002", "/auth")).not.toMatch(/\/$/u);
  });
});

describe("BASE_PATH", () => {
  it("is empty under the default build, so nothing changes with AUTH_BASE_PATH unset", () => {
    // Vitest reports `import.meta.env.BASE_URL` as "/" — the same value a build
    // with no `base` produces.
    expect(BASE_PATH).toBe("");
  });

  it("agrees with what normalizeBasePath makes of Vite's own BASE_URL", () => {
    expect(BASE_PATH).toBe(normalizeBasePath(import.meta.env.BASE_URL));
  });
});

describe("AUTH_BASE_PATH_ENV_KEY", () => {
  it("names the one knob the Dockerfile, compose file and CI all spell", () => {
    expect(AUTH_BASE_PATH_ENV_KEY).toBe("AUTH_BASE_PATH");
  });
});
