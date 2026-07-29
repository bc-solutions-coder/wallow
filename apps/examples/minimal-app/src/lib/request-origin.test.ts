import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { resolveRequestOrigin } from "./request-origin";

/**
 * This app's copy of the SSR origin derivation (Wallow-vufu.4.3).
 *
 * Node project: pure string work over a `Request`, no DOM.
 *
 * The full contract — proxy-chain header shapes, untrusted values, and the guard
 * that keeps all three copies byte-identical — lives in wallow-web's
 * `src/lib/request-origin.test.ts`. What this file owes is proof that THIS app's
 * copy honors both acceptance cases and that its `start.ts` routes through it.
 *
 * This app passthrough-proxies at its own root, so the origin IS the SDK's base
 * URL with nothing appended — which makes the scheme the only thing standing
 * between an SSR query key and its browser counterpart.
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
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/", "https"))).toBe(
      "https://wallow.dev",
    );
  });

  it("leaves the origin unchanged when no proxy reported a scheme", () => {
    expect(resolveRequestOrigin(requestWith("http://localhost:3000/"))).toBe(
      "http://localhost:3000",
    );
  });
});

describe("src/start.ts wiring", () => {
  const source: string = readFileSync(resolve(libDir, "..", "start.ts"), "utf8");

  it("derives the per-request SDK's origin through the helper", () => {
    expect(source).toMatch(/from\s+"\.\/lib\/request-origin"/u);
    expect(source).toMatch(/resolveRequestOrigin\(request\)/u);
  });

  it("no longer reads the origin straight off the request URL", () => {
    expect(source).not.toMatch(/new URL\(request\.url\)\.origin/u);
  });

  it("still hands that origin to the SDK as the whole base URL", () => {
    // No mount prefix here: the passthrough answers `/v1/**` at the root.
    expect(source).toMatch(/baseUrl:\s*requestOrigin/u);
  });
});
