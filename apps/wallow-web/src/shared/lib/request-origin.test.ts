import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { resolveRequestOrigin } from "./request-origin";

/**
 * The SSR origin derivation behind the SDK's `baseUrl`.
 *
 * Behind an HTTPS-terminating ingress the app is reached over plain HTTP, so an
 * SSR pass reading the request URL derives `http://…` while the browser derives
 * `https://…`. Generated query keys embed that `baseUrl` verbatim, so the two
 * never match and every SSR-prefetched query refetches on hydration. The helper
 * is a verbatim copy per Start app, so the drift guard lives here too.
 */

const libDir: string = dirname(fileURLToPath(import.meta.url));
const appsDir: string = resolve(libDir, "..", "..", "..", "..");

function requestWith(url: string, forwardedProto?: string): Request {
  const headers: Headers = new Headers();
  if (forwardedProto !== undefined) {
    headers.set("x-forwarded-proto", forwardedProto);
  }
  return new Request(url, { headers });
}

describe("resolveRequestOrigin", () => {
  it("uses the scheme a terminating proxy reports, not the one it reached us on", () => {
    // HTTPS at the edge, plain HTTP to the app.
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", "https"))).toBe(
      "https://wallow.dev",
    );
  });

  it("leaves the origin unchanged when no proxy reported a scheme", () => {
    // Direct-to-app deployments and local `pnpm dev` reach the app on the
    // scheme the browser used, so there is nothing to correct.
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/x"))).toBe("http://wallow.dev");
  });

  it("keeps a non-default port, since the origin is host and not hostname", () => {
    // `pnpm dev` and both compose stacks publish this app on an explicit port;
    // dropping it would point the SDK at :80.
    expect(resolveRequestOrigin(requestWith("http://localhost:3000/x", "https"))).toBe(
      "https://localhost:3000",
    );
  });

  it("returns the origin only — never the request's path or query", () => {
    expect(resolveRequestOrigin(requestWith("https://wallow.dev/dashboard?tab=apps"))).toBe(
      "https://wallow.dev",
    );
  });

  it("is a no-op when the reported scheme already matches the request's own", () => {
    expect(resolveRequestOrigin(requestWith("https://wallow.dev/x", "https"))).toBe(
      "https://wallow.dev",
    );
  });

  describe("header values a real ingress sends", () => {
    it("takes the first entry when a proxy chain comma-joined the header", () => {
      // Each hop appends, so the left-most entry is the scheme the browser used.
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", "https, http"))).toBe(
        "https://wallow.dev",
      );
    });

    it("tolerates the trailing colon of a `url.protocol`-shaped value", () => {
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", "https:"))).toBe(
        "https://wallow.dev",
      );
    });

    it("tolerates an upper-case scheme", () => {
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", "HTTPS"))).toBe(
        "https://wallow.dev",
      );
    });

    it("tolerates surrounding whitespace", () => {
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", "  https  "))).toBe(
        "https://wallow.dev",
      );
    });

    it.each([
      ["empty, as a misconfigured proxy sets it", ""],
      ["whitespace only", "   "],
    ])("falls back to the request's own scheme when the header is %s", (_label, value: string) => {
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", value))).toBe(
        "http://wallow.dev",
      );
    });
  });

  describe("untrusted header values", () => {
    // Nothing strips this header on a deployment whose ingress does not
    // overwrite it, so its value is attacker-supplied. It reaches the SDK's
    // `baseUrl` and every generated query key built from it, so an unrecognized
    // scheme must be inert rather than merely unhelpful.
    it.each([
      ["a scheme this app is never served over", "ftp"],
      ["a scheme that would execute if fetched", "javascript"],
      ["a value carrying an authority of its own", "https://evil.example"],
      ["a value carrying a credential separator", "https@evil.example"],
      ["a value with an embedded space", "ht tps"],
    ])("ignores %s and keeps the request's own scheme", (_label, value: string) => {
      expect(resolveRequestOrigin(requestWith("http://wallow.dev/x", value))).toBe(
        "http://wallow.dev",
      );
    });
  });
});

describe("src/app/start.ts wiring", () => {
  // `start.ts` is app-zone (it is the host entry) and this helper is
  // shared-zone, hence the hop out of `shared/lib/` and into `app/`.
  const source: string = readFileSync(resolve(libDir, "..", "..", "app", "start.ts"), "utf8");

  it("derives the per-request SDK's origin through the helper", () => {
    expect(source).toMatch(/from\s+"@shared\/lib\/request-origin"/u);
    expect(source).toMatch(/resolveRequestOrigin\(request\)/u);
  });

  it("no longer reads the origin straight off the request URL", () => {
    expect(source).not.toMatch(/new URL\(request\.url\)\.origin/u);
  });

  it("still mounts the BFF proxy prefix onto that origin", () => {
    // `/api` is what makes this app's baseUrl the BFF token tunnel rather than
    // the bare origin, so deriving the scheme must not drop it.
    expect(source).toMatch(/baseUrl:\s*`\$\{requestOrigin\}\$\{API_MOUNT\}`/u);
  });
});

describe("the copy in every other Start app", () => {
  // The helper is duplicated because `src/app/start.ts` lands in the client
  // bundle and may not import a Node-only module; the copies must not drift.
  const canonical: string = readFileSync(resolve(libDir, "request-origin.ts"), "utf8");

  it.each([
    // wallow-auth is zoned like this app, so its copy is `shared/lib`;
    // minimal-app is not, so its copy is still directly under `src/lib`.
    ["apps/wallow-auth", "wallow-auth/src/shared/lib/request-origin.ts"],
    ["apps/examples/minimal-app", "examples/minimal-app/src/lib/request-origin.ts"],
  ])("is byte-identical in %s", (_label: string, relativePath: string) => {
    expect(readFileSync(resolve(appsDir, relativePath), "utf8")).toBe(canonical);
  });
});
