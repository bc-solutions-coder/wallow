import { describe, expect, it } from "vitest";

import { applyForwardedHeaders } from "./forwarded";

/**
 * The shared `X-Forwarded-*` rules, tested directly rather than only through
 * the two proxies that call them. Both hops must behave identically here — a
 * divergence between them is the bug this module exists to prevent.
 */

/** The upstream leg's inbound URL for a request that arrived over plain HTTP. */
const PLAIN_INBOUND: URL = new URL("http://app.internal:3000/api/users");

/** The upstream leg's inbound URL for a request that arrived over TLS. */
const SECURE_INBOUND: URL = new URL("https://app.wallow.dev/api/users");

/** A caller-supplied client-IP header: nothing reads it, and it must not reach upstream. */
const STRIPPED_CLIENT_IP_HEADER: string = "x-wallow-client-ip";

/** No peer address known for the hop. */
const NO_CLIENT: undefined = undefined;

describe("applyForwardedHeaders", () => {
  it("derives X-Forwarded-Proto and X-Forwarded-Host from the inbound URL when the client sent neither", () => {
    const headers: Headers = new Headers();

    applyForwardedHeaders(headers, PLAIN_INBOUND, NO_CLIENT);

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
    applyForwardedHeaders(headers, PLAIN_INBOUND, NO_CLIENT);

    expect(headers.get("x-forwarded-proto")).toBe("https");
    expect(headers.get("x-forwarded-host")).toBe("wallow.dev");
  });

  it("derives the proto from a TLS inbound leg", () => {
    const headers: Headers = new Headers();

    applyForwardedHeaders(headers, SECURE_INBOUND, NO_CLIENT);

    expect(headers.get("x-forwarded-proto")).toBe("https");
    expect(headers.get("x-forwarded-host")).toBe("app.wallow.dev");
  });

  it("starts the X-Forwarded-For chain with the resolved client address", () => {
    const headers: Headers = new Headers();

    applyForwardedHeaders(headers, PLAIN_INBOUND, "203.0.113.7");

    expect(headers.get("x-forwarded-for")).toBe("203.0.113.7");
  });

  it("appends the resolved client address to an existing X-Forwarded-For chain", () => {
    const headers: Headers = new Headers({ "x-forwarded-for": "198.51.100.9" });

    // Append, never overwrite: the API pops the rightmost entry, and the outer
    // ingress's entries stay ahead of it for anyone reading the whole chain.
    applyForwardedHeaders(headers, PLAIN_INBOUND, "203.0.113.7");

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9, 203.0.113.7");
  });

  it("appends to a multi-hop chain without disturbing its order", () => {
    const headers: Headers = new Headers({ "x-forwarded-for": "198.51.100.9, 70.41.3.18" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, "203.0.113.7");

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9, 70.41.3.18, 203.0.113.7");
  });

  it("strips a client-IP header the caller sent so it cannot reach upstream", () => {
    const headers: Headers = new Headers({ [STRIPPED_CLIENT_IP_HEADER]: "203.0.113.7" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, NO_CLIENT);

    expect(headers.has(STRIPPED_CLIENT_IP_HEADER)).toBe(false);
    expect(headers.has("x-forwarded-for")).toBe(false);
  });

  it("leaves an inbound X-Forwarded-For chain alone when no address is given", () => {
    const headers: Headers = new Headers({ "x-forwarded-for": "198.51.100.9" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, NO_CLIENT);

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9");
  });

  it("writes no X-Forwarded-For when no peer address is known", () => {
    const headers: Headers = new Headers();

    // An unknown peer must not invent a chain entry — a bogus rightmost value
    // is worse for the rate limiter than none.
    applyForwardedHeaders(headers, PLAIN_INBOUND, NO_CLIENT);

    expect(headers.has("x-forwarded-for")).toBe(false);
  });

  it("ignores an empty address rather than appending a blank entry", () => {
    const headers: Headers = new Headers({ "x-forwarded-for": "198.51.100.9" });

    applyForwardedHeaders(headers, PLAIN_INBOUND, "");

    expect(headers.get("x-forwarded-for")).toBe("198.51.100.9");
  });
});
