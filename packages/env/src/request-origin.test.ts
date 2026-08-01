import { describe, expect, it } from "vitest";

import { resolveRequestOrigin } from "./request-origin";

/**
 * The SSR origin derivation behind the SDK's `baseUrl`.
 *
 * Behind an HTTPS-terminating ingress the app is reached over plain HTTP, so an
 * SSR pass reading the request URL derives `http://…` while the browser derives
 * `https://…`. Generated query keys embed that `baseUrl` verbatim, so the two
 * never match and every SSR-prefetched query refetches on hydration.
 */

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
    // Direct-to-app deployments reach it on the scheme the browser used.
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/x"))).toBe("http://wallow.dev");
  });

  it("keeps a non-default port, since the origin is host and not hostname", () => {
    // `pnpm dev` and both compose stacks publish these apps on explicit ports;
    // dropping one would point the SDK at :80.
    expect(resolveRequestOrigin(requestWith("http://localhost:3000/x", "https"))).toBe(
      "https://localhost:3000",
    );
  });

  it("keeps a non-default port when no proxy reported a scheme either", () => {
    expect(resolveRequestOrigin(requestWith("http://localhost:3002/login"))).toBe(
      "http://localhost:3002",
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
    // An ingress that does not overwrite this header leaves its value
    // attacker-supplied, and it reaches the SDK's `baseUrl`.
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
