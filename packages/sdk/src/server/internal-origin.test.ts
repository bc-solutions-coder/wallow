import { describe, expect, it } from "vitest";

import * as serverEntry from "./index";
import { INTERNAL_ORIGIN_ENV_KEY, resolveInternalOrigin } from "./internal-origin";

/**
 * Spec (f) of Wallow-pu6a.3.5: the SDK's server entry owns internal-origin
 * resolution.
 *
 * These cases are ported from `apps/wallow-web/src/lib/ssr-origin.test.ts`
 * (`resolveSsrInternalOrigin`, Wallow-spb5). Ownership moves here FIRST because
 * Phase 3 deletes wallow-web's `ssr-origin.ts` while the D13b bridge still needs
 * these values — the app must have somewhere to import them from before its own
 * copy goes away.
 *
 * The bug the logic exists for: `docker/docker-compose.test.yml` publishes
 * wallow-web as `127.0.0.1:5053:3000`, so during SSR the container derives
 * `http://localhost:5053` from the request `Host` and self-fetches it —
 * ECONNREFUSED inside the container, a 500 error boundary, no `data-app-ready`,
 * and `e2e-cross-app/login-journey.spec.ts` times out. `PORT` (3000) is the
 * listener the host actually binds, so `http://localhost:3000` is reachable.
 *
 * NEW vs. the wallow-web original: the second parameter. `resolveSsrInternalOrigin`
 * returned `undefined` and left the caller to fall back to the request origin;
 * `resolveInternalOrigin(env, requestOrigin)` takes that fallback directly, so a
 * host can hand the result straight to `createWallowSdk({ internalOrigin })`. With
 * no `requestOrigin` the ported cases behave exactly as before.
 */

const PUBLISHED_ORIGIN = "http://localhost:5053";
const INTERNAL_ORIGIN = "http://localhost:3000";

describe("resolveInternalOrigin (ported from resolveSsrInternalOrigin)", () => {
  it("derives the container's own listener from PORT when the published port differs", () => {
    expect(resolveInternalOrigin({ PORT: "3000" })).toBe(INTERNAL_ORIGIN);
  });

  it("tracks a non-default PORT rather than assuming 3000", () => {
    expect(resolveInternalOrigin({ PORT: "8080" })).toBe("http://localhost:8080");
  });

  it("lets the explicit override win over PORT", () => {
    expect(
      resolveInternalOrigin({
        PORT: "3000",
        [INTERNAL_ORIGIN_ENV_KEY]: "http://wallow-web:3000",
      }),
    ).toBe("http://wallow-web:3000");
  });

  it("normalizes a trailing slash off the explicit override so no '//api' target is built", () => {
    expect(resolveInternalOrigin({ [INTERNAL_ORIGIN_ENV_KEY]: "http://wallow-web:3000/" })).toBe(
      "http://wallow-web:3000",
    );
  });

  it("treats an empty override as unset and falls through to PORT", () => {
    expect(resolveInternalOrigin({ PORT: "3000", [INTERNAL_ORIGIN_ENV_KEY]: "" })).toBe(
      INTERNAL_ORIGIN,
    );
  });

  it("returns undefined when neither knob is set and no request origin is offered", () => {
    expect(resolveInternalOrigin({})).toBeUndefined();
  });

  it("treats an empty PORT as unset", () => {
    expect(resolveInternalOrigin({ PORT: "" })).toBeUndefined();
  });

  it("ignores a non-numeric PORT instead of building an unfetchable origin", () => {
    expect(resolveInternalOrigin({ PORT: "not-a-port" })).toBeUndefined();
  });

  it("keeps the env key wallow-web already configures", () => {
    // Renaming this key would silently drop the override out of every existing
    // docker-compose/.env, so the ported name is part of the contract.
    expect(INTERNAL_ORIGIN_ENV_KEY).toBe("WALLOW_WEB_INTERNAL_URL");
  });
});

describe("resolveInternalOrigin request-origin fallback", () => {
  it("falls back to the request origin when the environment says nothing", () => {
    expect(resolveInternalOrigin({}, PUBLISHED_ORIGIN)).toBe(PUBLISHED_ORIGIN);
  });

  it("still prefers the explicit override over the request origin", () => {
    expect(
      resolveInternalOrigin(
        { [INTERNAL_ORIGIN_ENV_KEY]: "http://wallow-web:3000" },
        PUBLISHED_ORIGIN,
      ),
    ).toBe("http://wallow-web:3000");
  });

  it("still prefers PORT over the request origin", () => {
    expect(resolveInternalOrigin({ PORT: "3000" }, PUBLISHED_ORIGIN)).toBe(INTERNAL_ORIGIN);
  });

  it("normalizes a trailing slash off the request origin", () => {
    expect(resolveInternalOrigin({}, "http://localhost:5053/")).toBe(PUBLISHED_ORIGIN);
  });

  it("treats an empty request origin as absent", () => {
    expect(resolveInternalOrigin({}, "")).toBeUndefined();
  });
});

describe("server entry export surface", () => {
  it("exports resolveInternalOrigin from ./server", () => {
    const entry: Record<string, unknown> = serverEntry as unknown as Record<string, unknown>;

    expect(entry.resolveInternalOrigin).toBe(resolveInternalOrigin);
  });

  it("exports the env key from ./server", () => {
    const entry: Record<string, unknown> = serverEntry as unknown as Record<string, unknown>;

    expect(entry.INTERNAL_ORIGIN_ENV_KEY).toBe(INTERNAL_ORIGIN_ENV_KEY);
  });
});
