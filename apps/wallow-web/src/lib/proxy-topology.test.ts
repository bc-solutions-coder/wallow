import { describe, expect, it } from "vitest";

import { isBffProxyPath } from "./proxy-topology";

/**
 * Spec (Wallow-ffpq.3.8): the reverse-proxy topology for wallow-web differs from
 * wallow-auth's proxy. This pins the two acceptance criteria as a table over the
 * single-source-of-truth predicate both hosts (server.ts, dev-server.ts) consume:
 *
 *   1. NO `/_blazor` route at all — the SignalR WebSocket-upgrade path disappears
 *      entirely with the Blazor circuit, so it is never a BFF/proxy path and
 *      falls through like any other unknown path.
 *   2. `/api/**` IS a BFF-owned path (answered by `bff-server.ts`'s bearer-token-
 *      attaching `createApiProxy`), NOT reverse-proxied verbatim to Wallow.Api.
 *      The bearer-attach behavior itself is pinned in `bff-server.test.ts`; this
 *      file pins that `/api/**` routes to the BFF and NOT to the router SSR.
 *
 * NOTE (RED): `proxy-topology.ts` is a signatures-only scaffold whose predicate
 * throws "not implemented", so every case below fails; the GREEN phase of this
 * bead implements the predicate and rewires both hosts' inline `isBffRequest`
 * to delegate to it.
 */
describe("wallow-web reverse-proxy topology", () => {
  describe("answers the BFF surface", () => {
    it.each([
      ["/health", "the E2E fixture liveness route"],
      ["/bff/login", "the OIDC login tunnel"],
      ["/bff/callback", "the OIDC callback tunnel"],
      ["/bff/user", "the sealed-session reflection"],
      ["/bff/logout", "the OIDC logout tunnel"],
    ])("routes %s to the BFF bridge (%s)", (pathname) => {
      expect(isBffProxyPath(pathname)).toBe(true);
    });

    it.each([["/api/notifications"], ["/api/identity/users/me"], ["/api/announcements?page=1"]])(
      "routes %s to the BFF's own bearer-attaching proxy, not a verbatim Wallow.Api passthrough",
      (pathname) => {
        // The path being a BFF path is the topology: wallow-web has NO raw
        // reverse-proxy of /api to Wallow.Api. A naive copy of wallow-auth's
        // proxy would add that second passthrough — this asserts it is absent.
        expect(isBffProxyPath(pathname)).toBe(true);
      },
    );
  });

  describe("has no /_blazor route (topology change: the SignalR circuit is gone)", () => {
    it.each([["/_blazor"], ["/_blazor/negotiate"], ["/_blazor/initializers"]])(
      "does not route %s to the BFF bridge — it is not a proxy path at all",
      (pathname) => {
        expect(isBffProxyPath(pathname)).toBe(false);
      },
    );
  });

  describe("falls through to the router SSR for everything else", () => {
    it.each([
      ["/", "the public home route"],
      ["/dashboard", "an SSR app route"],
      ["/client.js", "a built static asset served by the host, not the BFF"],
      ["/apibundle.js", "a path that only shares the /api PREFIX but is not the /api subtree"],
    ])("does not route %s to the BFF bridge (%s)", (pathname) => {
      expect(isBffProxyPath(pathname)).toBe(false);
    });
  });
});
