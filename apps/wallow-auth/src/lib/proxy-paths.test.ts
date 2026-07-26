import { describe, expect, it } from "vitest";

import { isProxyRequest } from "./proxy-paths";

/**
 * Spec (Wallow-xhw3): the reverse-proxy path topology for wallow-auth, lifted
 * into a single source of truth so `dev-server.ts` and `server.ts` stop keeping
 * divergent private copies (the root cause of the Dev discovery/JWKS 404).
 *
 * The acceptance criteria this table pins:
 *   1. `/.well-known/**` IS a proxy path — Dev OIDC discovery
 *      (`/.well-known/openid-configuration`) and the JWKS the discovery document
 *      advertises at the same origin (`/.well-known/jwks`) must reach the proxy,
 *      not fall through to router SSR (which 404s them as text/html). This is
 *      the prefix `dev-server.ts` omitted and `server.ts` already had.
 *   2. The pre-existing surface is unchanged — `/health`, `/v1/**`, `/connect/**`
 *      still route to the proxy.
 *   3. Every auth route and asset still falls through to SSR — `/login`, `/`,
 *      `/client.js` etc. are NOT proxy paths.
 *
 * NOTE (RED): `proxy-paths.ts` is a signatures-only scaffold whose predicate
 * throws "not implemented", so every case below fails; the GREEN phase
 * implements the predicate and rewires both hosts' inline `isProxyRequest` to
 * delegate to it.
 */
describe("wallow-auth reverse-proxy path topology", () => {
  describe("routes the OIDC discovery surface to the proxy (the Wallow-xhw3 fix)", () => {
    it.each([
      ["/.well-known/openid-configuration", "Dev OIDC discovery document"],
      ["/.well-known/jwks", "the JWKS the discovery document advertises"],
      ["/.well-known/", "the /.well-known subtree root"],
      ["/.well-known/oauth-authorization-server", "any other well-known metadata"],
    ])("routes %s to the reverse-proxy bridge (%s)", (pathname) => {
      expect(isProxyRequest(pathname)).toBe(true);
    });
  });

  describe("keeps the pre-existing proxy surface", () => {
    it.each([
      ["/health", "the liveness route"],
      ["/v1/ping", "the API surface"],
      ["/v1/identity/users/me", "a nested API path"],
      ["/connect/token", "the OIDC token endpoint"],
      ["/connect/authorize", "the OIDC authorize endpoint"],
    ])("routes %s to the reverse-proxy bridge (%s)", (pathname) => {
      expect(isProxyRequest(pathname)).toBe(true);
    });
  });

  describe("falls through to the router SSR for everything else", () => {
    it.each([
      ["/login", "an auth screen SSR route"],
      ["/", "the root redirect route"],
      ["/register", "another auth screen"],
      ["/client.js", "a built browser asset served by the host, not the proxy"],
      ["/v1", "a path that shares the /v1 PREFIX but is not the /v1/ subtree"],
      ["/health-check", "a path that shares the /health prefix but is not /health"],
      ["/.well-knownish", "a path that shares the /.well-known prefix but is not the subtree"],
    ])("does not route %s to the reverse-proxy bridge (%s)", (pathname) => {
      expect(isProxyRequest(pathname)).toBe(false);
    });
  });
});
