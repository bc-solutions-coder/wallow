import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import type { SsrRequestContext } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import {
  createSsrRequestContext,
  resolveSsrInternalOrigin,
  SSR_INTERNAL_ORIGIN_ENV_KEY,
} from "./ssr-origin";

/**
 * Spec (Wallow-spb5): wallow-web's SSR host must self-fetch an origin IT can
 * reach, not the browser-facing origin of the incoming request.
 *
 * The bug: `docker/docker-compose.test.yml` publishes wallow-web as
 * `127.0.0.1:5053:3000`, so during SSR of `/dashboard/apps` the container derives
 * `http://localhost:5053` from the request `Host` and self-fetches it —
 * ECONNREFUSED inside the container, a 500 error boundary, no `data-app-ready`,
 * and `e2e-cross-app/login-journey.spec.ts` times out on the post-landing
 * hydration wait. `PORT` (3000) is the listener the host actually binds, so
 * `http://localhost:3000` is the reachable origin.
 *
 * Resolution stays env-driven with an explicit override, mirroring wallow-auth's
 * `WALLOW_API_INTERNAL_URL` convention (empty value counts as unset), and yields
 * `undefined` when nothing indicates a split so `pnpm dev`/Aspire keep today's
 * working request-origin behavior.
 */

// apps/wallow-web/src/lib -> repo root (lib -> src -> wallow-web -> apps -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "..");

const PUBLISHED_ORIGIN = "http://localhost:5053";
const INTERNAL_ORIGIN = "http://localhost:3000";

describe("resolveSsrInternalOrigin", () => {
  it("derives the container's own listener from PORT when the published port differs", () => {
    expect(resolveSsrInternalOrigin({ PORT: "3000" })).toBe(INTERNAL_ORIGIN);
  });

  it("tracks a non-default PORT rather than assuming 3000", () => {
    expect(resolveSsrInternalOrigin({ PORT: "8080" })).toBe("http://localhost:8080");
  });

  it("lets the explicit override win over PORT", () => {
    expect(
      resolveSsrInternalOrigin({
        PORT: "3000",
        [SSR_INTERNAL_ORIGIN_ENV_KEY]: "http://wallow-web:3000",
      }),
    ).toBe("http://wallow-web:3000");
  });

  it("normalizes a trailing slash off the explicit override so no '//api' target is built", () => {
    expect(
      resolveSsrInternalOrigin({ [SSR_INTERNAL_ORIGIN_ENV_KEY]: "http://wallow-web:3000/" }),
    ).toBe("http://wallow-web:3000");
  });

  it("treats an empty override as unset and falls through to PORT", () => {
    expect(resolveSsrInternalOrigin({ PORT: "3000", [SSR_INTERNAL_ORIGIN_ENV_KEY]: "" })).toBe(
      INTERNAL_ORIGIN,
    );
  });

  it("returns undefined when neither knob is set, so the request origin is used", () => {
    expect(resolveSsrInternalOrigin({})).toBeUndefined();
  });

  it("treats an empty PORT as unset", () => {
    expect(resolveSsrInternalOrigin({ PORT: "" })).toBeUndefined();
  });

  it("ignores a non-numeric PORT instead of building an unfetchable origin", () => {
    expect(resolveSsrInternalOrigin({ PORT: "not-a-port" })).toBeUndefined();
  });
});

describe("createSsrRequestContext", () => {
  it("stamps the internal origin alongside the browser-facing request origin", () => {
    const context: SsrRequestContext = createSsrRequestContext(
      new Request(`${PUBLISHED_ORIGIN}/dashboard/apps`),
      { PORT: "3000" },
    );

    expect(context.origin).toBe(PUBLISHED_ORIGIN);
    expect(context.internalOrigin).toBe(INTERNAL_ORIGIN);
  });

  it("forwards the incoming session cookie", () => {
    const context: SsrRequestContext = createSsrRequestContext(
      new Request(`${PUBLISHED_ORIGIN}/dashboard/apps`, {
        headers: { cookie: "wallow_bff=sess" },
      }),
      { PORT: "3000" },
    );

    expect(context.cookie).toBe("wallow_bff=sess");
  });

  it("leaves the cookie undefined when the request carries none", () => {
    const context: SsrRequestContext = createSsrRequestContext(
      new Request(`${PUBLISHED_ORIGIN}/dashboard/apps`),
      { PORT: "3000" },
    );

    expect(context.cookie).toBeUndefined();
  });

  it("omits the internal origin when the environment gives no reason to override", () => {
    const context: SsrRequestContext = createSsrRequestContext(
      new Request("http://localhost:3000/dashboard/apps"),
      {},
    );

    expect(context.origin).toBe(INTERNAL_ORIGIN);
    expect(context.internalOrigin).toBeUndefined();
  });
});

describe("src/ssr.tsx wiring", () => {
  // ssr.tsx sits outside the app's typecheck include and cannot be imported here
  // (it owns the AsyncLocalStorage and the router request handler), so the wiring
  // is pinned at the source level: the per-request context must come from
  // createSsrRequestContext, not an inline request-origin literal.
  const source: string = readFileSync(resolve(repoRoot, "apps/wallow-web/src/ssr.tsx"), "utf8");

  it("builds the per-request context through createSsrRequestContext", () => {
    expect(source).toMatch(/createSsrRequestContext\(/u);
  });

  it("no longer derives the SSR fetch origin inline from the request URL", () => {
    expect(source).not.toMatch(/origin:\s*new URL\(request\.url\)\.origin/u);
  });
});
