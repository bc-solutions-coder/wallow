import { describe, expect, it } from "vitest";

import { INTERNAL_ORIGIN_ENV_KEY, resolveInternalOrigin } from "./internal-origin";

/**
 * Internal-origin resolution: the override, then `PORT`, then the optional
 * caller-supplied request origin.
 *
 * The bug the logic exists for: `docker/docker-compose.test.yml` publishes
 * wallow-web as `127.0.0.1:5053:3000`, so during SSR the container derives
 * `http://localhost:5053` from the request `Host` and self-fetches it —
 * ECONNREFUSED inside the container, a 500 error boundary, and no
 * `data-app-ready`. `PORT` (3000) is the listener the host actually binds.
 */

const PUBLISHED_ORIGIN = "http://localhost:5053";
const INTERNAL_ORIGIN = "http://localhost:3000";

describe("resolveInternalOrigin", () => {
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

  it("keeps the env key every compose file already configures", () => {
    // Renaming this key would silently drop the override out of every existing
    // docker-compose/.env, so the name is part of the contract.
    expect(INTERNAL_ORIGIN_ENV_KEY).toBe("WALLOW_WEB_INTERNAL_URL");
  });
});

describe("the optional request-origin arm", () => {
  // Every Start app omits it on purpose: the browser's origin is exactly the
  // address that is unreachable from inside a container publishing a different
  // host port.
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
