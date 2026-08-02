import { afterEach, describe, expect, it, vi } from "vitest";

/**
 * The wiring half of base-path support (Wallow-vufu.2.2): `base-path.test.ts`
 * proves the string arithmetic, this file proves the one piece of that wiring
 * that is observable without reading a file — the `base` Vite resolves from
 * `AUTH_BASE_PATH`, which is what puts the prefix on every emitted asset URL.
 *
 * The other four pins this file used to carry (the Start plugin's
 * `router.basepath`, nitro's `baseURL`, the SDK `baseUrl` on both the SSR and
 * browser sides, and the Dockerfile's build-time ARG) were source-text and
 * Dockerfile-text greps. They are deleted rather than relocated: a based build
 * either serves its assets or does not, and the suite that can tell is
 * `e2e/`, driven against the built Nitro server, not a regex over
 * `vite.config.ts`.
 *
 * Node project: it imports the Vite config and mounts nothing.
 */

describe("vite.config.ts", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  async function loadConfig(basePath?: string): Promise<Record<string, unknown>> {
    if (basePath === undefined) {
      vi.stubEnv("AUTH_BASE_PATH", "");
    } else {
      vi.stubEnv("AUTH_BASE_PATH", basePath);
    }
    vi.resetModules();
    const module = (await import("../vite.config")) as { default: Record<string, unknown> };
    return module.default;
  }

  it("serves at the root when AUTH_BASE_PATH is unset — the default fork behavior", async () => {
    const config = await loadConfig();

    // Either spelling is the Vite default; what must NOT happen is a prefix.
    expect(config.base ?? "/").toBe("/");
  });

  it("bases the build on AUTH_BASE_PATH so every emitted asset URL carries the prefix", async () => {
    const config = await loadConfig("/auth");

    expect(config.base).toBe("/auth/");
  });

  it("tolerates the prefix written without slashes, as a compose file will spell it", async () => {
    const config = await loadConfig("auth");

    expect(config.base).toBe("/auth/");
  });
});
