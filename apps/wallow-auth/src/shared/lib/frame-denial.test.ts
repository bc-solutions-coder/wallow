import { describe, expect, it } from "vitest";

import { withFrameDenial } from "./frame-denial";

/**
 * `withFrameDenial` stamps the anti-framing pair onto a response, appending to
 * a CSP the upstream already set rather than discarding it.
 */

describe("withFrameDenial", () => {
  it("sets both refusals on a response that has neither", () => {
    const response: Response = withFrameDenial(new Response("page"));

    expect(response.headers.get("X-Frame-Options")).toBe("DENY");
    expect(response.headers.get("Content-Security-Policy")).toBe("frame-ancestors 'none'");
  });

  it("appends frame-ancestors to a policy the response already carries", () => {
    const upstream = new Response("page", {
      headers: { "Content-Security-Policy": "default-src 'self'; script-src 'self'" },
    });

    const response: Response = withFrameDenial(upstream);

    // The API's own policy on a proxied page is kept — replacing it would relax
    // every other directive to say no to one thing.
    expect(response.headers.get("Content-Security-Policy")).toBe(
      "default-src 'self'; script-src 'self'; frame-ancestors 'none'",
    );
  });

  it("does not double a trailing separator", () => {
    const upstream = new Response("page", {
      headers: { "Content-Security-Policy": "default-src 'self';" },
    });

    expect(withFrameDenial(upstream).headers.get("Content-Security-Policy")).toBe(
      "default-src 'self'; frame-ancestors 'none'",
    );
  });

  it("leaves a policy that already states frame-ancestors alone", () => {
    const upstream = new Response("page", {
      headers: { "Content-Security-Policy": "frame-ancestors 'self'" },
    });

    expect(withFrameDenial(upstream).headers.get("Content-Security-Policy")).toBe(
      "frame-ancestors 'self'",
    );
  });

  it("copies an immutable response rather than failing on it", async () => {
    // A response with locked headers, as `fetch` hands back.
    const upstream = new Response("upstream body", {
      status: 302,
      statusText: "Found",
      headers: { Location: "/consent", "Set-Cookie": "sid=1; HttpOnly" },
    });
    Object.defineProperty(upstream.headers, "set", {
      value: () => {
        throw new TypeError("immutable");
      },
    });

    const response: Response = withFrameDenial(upstream);

    expect(response).not.toBe(upstream);
    expect(response.status).toBe(302);
    expect(response.statusText).toBe("Found");
    expect(response.headers.get("Location")).toBe("/consent");
    expect(response.headers.get("Set-Cookie")).toBe("sid=1; HttpOnly");
    expect(response.headers.get("X-Frame-Options")).toBe("DENY");
    expect(response.headers.get("Content-Security-Policy")).toBe("frame-ancestors 'none'");
    await expect(response.text()).resolves.toBe("upstream body");
  });
});
