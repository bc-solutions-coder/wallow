import { describe, expect, it } from "vitest";

import {
  parseTrustedProxies,
  type PeerRequest,
  TRUST_NO_PROXIES,
  TRUSTED_PROXIES_ENV_KEY,
} from "./client-address";
import { createRequestOriginResolver, resolveRequestOrigin } from "./request-origin";

/**
 * The SSR origin derivation behind the SDK's `baseUrl`.
 *
 * Behind an HTTPS-terminating ingress the app is reached over plain HTTP, so an
 * SSR pass reading the request URL derives `http://…` while the browser derives
 * `https://…`. Generated query keys embed that `baseUrl` verbatim, so the two
 * never match and every SSR-prefetched query refetches on hydration.
 *
 * `x-forwarded-proto` is believed only when the immediate peer is a configured
 * proxy — the same gate `resolveClientAddress` puts on `x-forwarded-for`, so the
 * two forwarded headers are one trust policy rather than two.
 */

const TRUSTED = parseTrustedProxies("10.0.0.0/8");

/** A peer inside the trusted block — the ingress the header is believed from. */
const PROXY_PEER = "10.0.0.7";

/** A peer outside every trusted block — a caller speaking for itself. */
const OUTSIDE_PEER = "203.0.113.9";

function requestWith(url: string, forwardedProto?: string): Request {
  const headers: Headers = new Headers();
  if (forwardedProto !== undefined) {
    headers.set("x-forwarded-proto", forwardedProto);
  }
  return new Request(url, { headers });
}

function peerRequestWith(
  url: string,
  peer: string | undefined,
  forwardedProto?: string,
): PeerRequest {
  // srvx hands the peer address in on an extra property of a real `Request`,
  // which is what this reproduces — the object is not a stand-in.
  return Object.assign(requestWith(url, forwardedProto), { ip: peer }) as PeerRequest;
}

describe("resolveRequestOrigin", () => {
  it("uses the scheme a trusted proxy reports, not the one it reached us on", () => {
    // HTTPS at the edge, plain HTTP to the app.
    expect(
      resolveRequestOrigin(requestWith("http://wallow.dev/x", "https"), PROXY_PEER, TRUSTED),
    ).toBe("https://wallow.dev");
  });

  it("leaves the origin unchanged when no proxy reported a scheme", () => {
    // Direct-to-app deployments reach it on the scheme the browser used.
    expect(resolveRequestOrigin(requestWith("http://wallow.dev/x"), PROXY_PEER, TRUSTED)).toBe(
      "http://wallow.dev",
    );
  });

  it("keeps a non-default port, since the origin is host and not hostname", () => {
    // `pnpm dev` and both compose stacks publish these apps on explicit ports;
    // dropping one would point the SDK at :80.
    expect(
      resolveRequestOrigin(requestWith("http://localhost:3000/x", "https"), PROXY_PEER, TRUSTED),
    ).toBe("https://localhost:3000");
  });

  it("keeps a non-default port when no proxy reported a scheme either", () => {
    expect(
      resolveRequestOrigin(requestWith("http://localhost:3002/login"), PROXY_PEER, TRUSTED),
    ).toBe("http://localhost:3002");
  });

  it("returns the origin only — never the request's path or query", () => {
    expect(
      resolveRequestOrigin(
        requestWith("https://wallow.dev/dashboard?tab=apps"),
        PROXY_PEER,
        TRUSTED,
      ),
    ).toBe("https://wallow.dev");
  });

  it("is a no-op when the reported scheme already matches the request's own", () => {
    expect(
      resolveRequestOrigin(requestWith("https://wallow.dev/x", "https"), PROXY_PEER, TRUSTED),
    ).toBe("https://wallow.dev");
  });

  describe("the trusted-peer gate", () => {
    it("ignores the header when the peer is not a trusted proxy", () => {
      // Any caller can send `x-forwarded-proto`; only a configured ingress may
      // rewrite the origin the SDK builds its query keys from.
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", "https"), OUTSIDE_PEER, TRUSTED),
      ).toBe("http://wallow.dev");
    });

    it("ignores the header when nothing is configured, which is the default", () => {
      expect(
        resolveRequestOrigin(
          requestWith("http://wallow.dev/x", "https"),
          PROXY_PEER,
          TRUST_NO_PROXIES,
        ),
      ).toBe("http://wallow.dev");
    });

    it("ignores the header when the host reported no peer to judge", () => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", "https"), undefined, TRUSTED),
      ).toBe("http://wallow.dev");
    });

    it("ignores the header when the peer does not parse as an address", () => {
      expect(
        resolveRequestOrigin(
          requestWith("http://wallow.dev/x", "https"),
          "not-an-address",
          TRUSTED,
        ),
      ).toBe("http://wallow.dev");
    });

    it("trusts a v4 peer that a dual-stack listener reported in mapped form", () => {
      // Node reports the same client as `::ffff:10.0.0.7` or `10.0.0.7`
      // depending on how the socket bound; the gate must not care which.
      expect(
        resolveRequestOrigin(
          requestWith("http://wallow.dev/x", "https"),
          "::ffff:10.0.0.7",
          TRUSTED,
        ),
      ).toBe("https://wallow.dev");
    });
  });

  describe("header values a real ingress sends", () => {
    it("takes the first entry when a proxy chain comma-joined the header", () => {
      // Each hop appends, so the left-most entry is the scheme the browser used.
      expect(
        resolveRequestOrigin(
          requestWith("http://wallow.dev/x", "https, http"),
          PROXY_PEER,
          TRUSTED,
        ),
      ).toBe("https://wallow.dev");
    });

    it("tolerates the trailing colon of a `url.protocol`-shaped value", () => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", "https:"), PROXY_PEER, TRUSTED),
      ).toBe("https://wallow.dev");
    });

    it("tolerates an upper-case scheme", () => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", "HTTPS"), PROXY_PEER, TRUSTED),
      ).toBe("https://wallow.dev");
    });

    it("tolerates surrounding whitespace", () => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", "  https  "), PROXY_PEER, TRUSTED),
      ).toBe("https://wallow.dev");
    });

    it.each([
      ["empty, as a misconfigured proxy sets it", ""],
      ["whitespace only", "   "],
    ])("falls back to the request's own scheme when the header is %s", (_label, value: string) => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", value), PROXY_PEER, TRUSTED),
      ).toBe("http://wallow.dev");
    });
  });

  describe("untrusted header values", () => {
    // Even a trusted proxy's header is allowlisted, never sanitized: a
    // misconfigured ingress can forward what a caller sent, and the value
    // reaches the SDK's `baseUrl`.
    it.each([
      ["a scheme this app is never served over", "ftp"],
      ["a scheme that would execute if fetched", "javascript"],
      ["a value carrying an authority of its own", "https://evil.example"],
      ["a value carrying a credential separator", "https@evil.example"],
      ["a value with an embedded space", "ht tps"],
    ])("ignores %s and keeps the request's own scheme", (_label, value: string) => {
      expect(
        resolveRequestOrigin(requestWith("http://wallow.dev/x", value), PROXY_PEER, TRUSTED),
      ).toBe("http://wallow.dev");
    });
  });
});

describe("createRequestOriginResolver", () => {
  it("reads the trusted list from the env record it is handed", () => {
    const resolve = createRequestOriginResolver({ [TRUSTED_PROXIES_ENV_KEY]: "10.0.0.0/8" });

    expect(resolve(peerRequestWith("http://wallow.dev/x", PROXY_PEER, "https"))).toBe(
      "https://wallow.dev",
    );
  });

  it("trusts nothing when the variable is absent from the record", () => {
    const resolve = createRequestOriginResolver({});

    expect(resolve(peerRequestWith("http://wallow.dev/x", PROXY_PEER, "https"))).toBe(
      "http://wallow.dev",
    );
  });

  it("judges the peer the host handed in on the request", () => {
    const resolve = createRequestOriginResolver({ [TRUSTED_PROXIES_ENV_KEY]: "10.0.0.0/8" });

    expect(resolve(peerRequestWith("http://wallow.dev/x", OUTSIDE_PEER, "https"))).toBe(
      "http://wallow.dev",
    );
  });
});
