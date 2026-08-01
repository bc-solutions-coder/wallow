import { normalizeBasePath } from "@bc-solutions-coder/env/base-path";
import { describe, expect, it } from "vitest";

import { AUTH_BASE_PATH_ENV_KEY, BASE_PATH, toAppHref } from "./base-path";

/**
 * The app-owned half of base-path support: what THIS build's prefix is, and the
 * href helper that defaults to it. The string arithmetic is
 * `packages/env/src/base-path.test.ts`.
 *
 * Node project: pure string work, no DOM.
 */

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

describe("toAppHref", () => {
  it("defaults to this build's base path, which is what every call site relies on", () => {
    expect(toAppHref("/login")).toBe(`${BASE_PATH}/login`);
  });

  it("prefixes an explicitly supplied base path", () => {
    expect(toAppHref("/login", "/auth")).toBe("/auth/login");
  });

  it("leaves the href alone when there is no prefix", () => {
    expect(toAppHref("/login", "")).toBe("/login");
  });
});

describe("AUTH_BASE_PATH_ENV_KEY", () => {
  it("names the one knob the Dockerfile, compose file and CI all spell", () => {
    expect(AUTH_BASE_PATH_ENV_KEY).toBe("AUTH_BASE_PATH");
  });
});
