import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { BASE_PATH, withBasePath } from "./base-path";
import { resolveRequestOrigin } from "./request-origin";

/**
 * This app's copy of the SSR origin derivation (Wallow-vufu.4.3).
 *
 * Node project: pure string work over a `Request`, no DOM.
 *
 * The full contract — proxy-chain header shapes, untrusted values, and the guard
 * that keeps all three copies byte-identical — lives in wallow-web's
 * `src/lib/request-origin.test.ts`. What this file owes is proof that THIS app's
 * copy honors both acceptance cases and that its `start.ts` actually routes
 * through it, composed with the base path the passthrough answers under.
 */

const libDir: string = dirname(fileURLToPath(import.meta.url));

function requestWith(url: string, forwardedProto?: string): Request {
  const headers: Headers = new Headers();
  if (forwardedProto !== undefined) {
    headers.set("x-forwarded-proto", forwardedProto);
  }
  return new Request(url, { headers });
}

describe("resolveRequestOrigin", () => {
  it("uses the scheme a terminating proxy reports, not the one it reached us on", () => {
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/login", "https"))).toBe(
      "https://wallow.dev",
    );
  });

  it("leaves the origin unchanged when no proxy reported a scheme", () => {
    expect(resolveRequestOrigin(requestWith("http://localhost:3002/login"))).toBe(
      "http://localhost:3002",
    );
  });

  it("still composes with the base path into the SDK's base URL", () => {
    // Under a based build the bare origin is a different app, so the scheme fix
    // has to survive `withBasePath` rather than replace it.
    const origin: string = resolveRequestOrigin(requestWith("http://wallow.dev/login", "https"));

    expect(withBasePath(origin, "/auth")).toBe("https://wallow.dev/auth");
    expect(withBasePath(origin, BASE_PATH)).toBe(`https://wallow.dev${BASE_PATH}`);
  });
});

describe("src/app/start.ts wiring", () => {
  // `libDir` is `src/shared/lib`, so the app zone is two hops up and over.
  const source: string = readFileSync(resolve(libDir, "..", "..", "app", "start.ts"), "utf8");

  it("derives the per-request SDK's origin through the helper", () => {
    // An alias, not a relative hop: `start.ts` is in the `app` zone now, and the
    // only legal way out of a zone is the alias.
    expect(source).toMatch(/from\s+"@shared\/lib\/request-origin"/u);
    expect(source).toMatch(/resolveRequestOrigin\(request\)/u);
  });

  it("no longer reads the origin straight off the request URL", () => {
    expect(source).not.toMatch(/new URL\(request\.url\)\.origin/u);
  });

  it("still prefixes that origin with the base path", () => {
    expect(source).toMatch(/baseUrl:\s*withBasePath\(requestOrigin,\s*BASE_PATH\)/u);
  });
});
