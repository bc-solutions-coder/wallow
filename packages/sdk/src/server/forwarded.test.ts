import { describe, expect, it } from "vitest";

import { applyForwardedHeaders, CLIENT_IP_HEADER } from "./forwarded";
import { CLIENT_IP_HEADER as PASSTHROUGH_CLIENT_IP_HEADER } from "./passthrough";

/**
 * The shared `X-Forwarded-*` rules, tested directly rather than only through
 * the two proxies that call them. Both hops must behave identically here — the
 * bug this module exists to fix (Wallow-vufu.4.2) was exactly a divergence
 * between them.
 */

/** The upstream leg's inbound URL for a request that arrived over plain HTTP. */
const PLAIN_INBOUND: URL = new URL("http://app.internal:3000/api/users");

/** The upstream leg's inbound URL for a request that arrived over TLS. */
const SECURE_INBOUND: URL = new URL("https://app.wallow.dev/api/users");

describe("CLIENT_IP_HEADER", () => {
  it("is the one seam header both proxies and every host agree on", () => {
    // The passthrough subpath publishes this constant to app hosts (wallow-auth
    // stamps it). A second, drifting copy would silently disable IP forwarding
    // on whichever side lost the race.
    expect(CLIENT_IP_HEADER).toBe(PASSTHROUGH_CLIENT_IP_HEADER);
    expect(CLIENT_IP_HEADER).toBe("x-wallow-client-ip");
  });
});

describe("applyForwardedHeaders", () => {
  it("derives X-Forwarded-Proto and X-Forwarded-Host from the inbound URL when the client sent neither", () => {
    const headers: Headers = new Headers();

    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    // `URL.protocol` carries a trailing colon; the header value must not.
    expect(headers.get("x-forwarded-proto")).toBe("http");
    expect(headers.get("x-forwarded-host")).toBe("app.internal:3000");
  });

  it("leaves an outer ingress's X-Forwarded-Proto and X-Forwarded-Host untouched", () => {
    const headers: Headers = new Headers({
      "x-forwarded-proto": "https",
      "x-forwarded-host": "wallow.dev",
    });

    // The inbound leg is plain HTTP, but the TLS-terminating ingress in front
    // is the only hop that knows the browser's real scheme: overwriting it
    // downgrades the API's view and trips OpenIddict's HTTPS check (ID2083).
    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.get("x-forwarded-proto")).toBe("https");
    expect(headers.get("x-forwarded-host")).toBe("wallow.dev");
  });

  it("derives the proto from a TLS inbound leg", () => {
    const headers: Headers = new Headers();

    applyForwardedHeaders(headers, SECURE_INBOUND, true);

    expect(headers.get("x-forwarded-proto")).toBe("https");
    expect(headers.get("x-forwarded-host")).toBe("app.wallow.dev");
  });

  it("starts the X-Forwarded-For chain with the stamped peer address", () => {
    const headers: Headers = new Headers({ [CLIENT_IP_HEADER]: "203.0.113.7" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.get("x-forwarded-for")).toBe("203.0.113.7");
  });

  it("appends the stamped peer address to an existing X-Forwarded-For chain", () => {
    const headers: Headers = new Headers({
      "x-forwarded-for": "198.51.100.9",
      [CLIENT_IP_HEADER]: "203.0.113.7",
    });

    // Append, never overwrite: the leftmost entry is the outer ingress's view
    // of the real client, which is the one the API's rate limiter wants.
    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9, 203.0.113.7");
  });

  it("appends to a multi-hop chain without disturbing its order", () => {
    const headers: Headers = new Headers({
      "x-forwarded-for": "198.51.100.9, 70.41.3.18",
      [CLIENT_IP_HEADER]: "203.0.113.7",
    });

    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9, 70.41.3.18, 203.0.113.7");
  });

  it("strips the seam header so it never reaches the upstream API", () => {
    const headers: Headers = new Headers({ [CLIENT_IP_HEADER]: "203.0.113.7" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.has(CLIENT_IP_HEADER)).toBe(false);
  });

  it("strips the seam header even when client-IP forwarding is disabled", () => {
    const headers: Headers = new Headers({ [CLIENT_IP_HEADER]: "203.0.113.7" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, false);

    expect(headers.has(CLIENT_IP_HEADER)).toBe(false);
    expect(headers.has("x-forwarded-for")).toBe(false);
  });

  it("leaves an inbound X-Forwarded-For chain alone when client-IP forwarding is disabled", () => {
    const headers: Headers = new Headers({
      "x-forwarded-for": "198.51.100.9",
      [CLIENT_IP_HEADER]: "203.0.113.7",
    });

    applyForwardedHeaders(headers, PLAIN_INBOUND, false);

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9");
  });

  it("writes no X-Forwarded-For when the host stamped no peer address", () => {
    const headers: Headers = new Headers();

    // An unstamped hop must not invent a chain entry — a bogus leftmost value
    // is worse for the rate limiter than none.
    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.has("x-forwarded-for")).toBe(false);
  });

  it("ignores an empty seam header rather than appending a blank entry", () => {
    const headers: Headers = new Headers({
      "x-forwarded-for": "198.51.100.9",
      [CLIENT_IP_HEADER]: "",
    });

    applyForwardedHeaders(headers, PLAIN_INBOUND, true);

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9");
    expect(headers.has(CLIENT_IP_HEADER)).toBe(false);
  });
});
