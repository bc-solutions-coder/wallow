/**
 * `getWallowAuthSdk()` facade — the guarded-singleton entry every wallow-auth
 * route/component uses to reach the API (Wallow-vec7.2.3).
 *
 * On first use it configures the shared `@hey-api` client for same-origin use
 * and wires the CSRF request interceptor exactly once; thereafter it returns the
 * same namespaced object. It composes two seams built in this phase:
 *
 *   - `auth` — the SDK's `createAuthClient()` facade (Wallow-vec7.2.1), one
 *     method per identity endpoint, unwrapping `{ data, error }` and throwing on
 *     failure so React Query surfaces the error.
 *   - `oidc` — the SDK's pure OIDC URL builders + return-url guard
 *     (Wallow-vec7.2.2), for the handshake steps the OpenAPI spec does not model.
 *
 * SAME-ORIGIN BASE URL: unlike wallow-web (a BFF token tunnel mounted under
 * `/api`), wallow-auth's h3 server is a passthrough reverse proxy that forwards
 * `/v1/**` and `/connect/**` verbatim at the ROOT (see
 * `src/lib/auth-server.ts`). The client's baseUrl is therefore `/`, not `/api`.
 * `configureBffClient` is reused rather than adding an auth-specific configure
 * call: it does exactly `setConfig({ baseUrl, credentials: 'include' })`, and
 * `credentials: 'include'` is what carries the auth cookie.
 *
 * CONVENTION: this file is the ONLY place in the app allowed to import from the
 * `@bc-solutions-coder/sdk` barrel. Routes and components reach mutations and the
 * OIDC builders through `getWallowAuthSdk()`, and reads through the SDK's
 * `./query` factories (which this module bootstraps).
 */

import {
  type AuthClient,
  buildConnectAuthorizeUrl,
  buildConnectLogoutUrl,
  buildConsentSubmitUrl,
  buildExchangeTicketUrl,
  client,
  configureBffClient,
  createAuthClient,
  createConfiguredOnce,
  isSafeReturnUrl,
  wireCsrfInterceptor,
} from "@bc-solutions-coder/sdk";
import { ensureQueryBootstrapped, registerQueryBootstrap } from "@bc-solutions-coder/sdk/query";

/**
 * The OIDC slice — the SDK's non-spec handshake helpers, re-exposed on the
 * singleton so callers reach them through the same seam as `auth`.
 */
export interface AuthOidcSlice {
  /** Mirrors the server-side `ReturnUrlValidator.IsSafe`: single leading `/`, not `//`. */
  isSafeReturnUrl: typeof isSafeReturnUrl;
  /** Build `${origin}/connect/authorize?${params}`. */
  buildConnectAuthorizeUrl: typeof buildConnectAuthorizeUrl;
  /** Append `consent_granted`/`consent_denied` to a validated returnUrl. */
  buildConsentSubmitUrl: typeof buildConsentSubmitUrl;
  /** Build the sign-in-ticket exchange URL. */
  buildExchangeTicketUrl: typeof buildExchangeTicketUrl;
  /** Build `${origin}/connect/logout` with an optional post-logout redirect. */
  buildConnectLogoutUrl: typeof buildConnectLogoutUrl;
}

/** The namespaced facade the singleton hands back. */
export interface WallowAuthSdk {
  /** Typed identity endpoints (SDK `createAuthClient()`). */
  auth: AuthClient;
  /** Pure OIDC URL builders and the open-redirect guard. */
  oidc: AuthOidcSlice;
}

/**
 * Point the shared `@hey-api` client at the same-origin root and wire the CSRF
 * request interceptor. Runs exactly once per module graph — see
 * {@link registerQueryBootstrap} below. There is NO SSR branch: wallow-auth's h3
 * server is a passthrough proxy, so `/` is the correct base URL in both passes.
 */
export function configureClient(): void {
  configureBffClient({ baseUrl: "/" });
  wireCsrfInterceptor(client);
}

// Register the configurator with the SDK query layer so `./query` factories
// configure the client lazily on the first query. Registration is side-effect
// free — nothing touches the client until a query (or the facade) runs.
registerQueryBootstrap(configureClient);

/**
 * Return the singleton facade, configuring the same-origin client and the CSRF
 * interceptor on first use. Every later call is a cheap no-op that hands back
 * the same instance. The one-time configure-then-build guard is the SDK's shared
 * {@link createConfiguredOnce} helper — built lazily, so merely importing this
 * module (in a test or an SSR pass) has no side effect on the shared client.
 *
 * The configure step DELEGATES to {@link ensureQueryBootstrapped} rather than
 * calling {@link configureClient} directly: the facade's guard and the query
 * bootstrap's guard are independent, so calling the configurator from both would
 * run it twice in whichever order the app hits them, and the second pass would
 * register a SECOND CSRF interceptor on the shared client.
 */
export const getWallowAuthSdk: () => WallowAuthSdk = createConfiguredOnce(
  ensureQueryBootstrapped,
  (): WallowAuthSdk => ({
    auth: createAuthClient(),
    oidc: {
      isSafeReturnUrl,
      buildConnectAuthorizeUrl,
      buildConsentSubmitUrl,
      buildExchangeTicketUrl,
      buildConnectLogoutUrl,
    },
  }),
);
