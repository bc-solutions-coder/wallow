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
 * CONVENTION: this file is the ONLY place in the app allowed to import from
 * `@bc-solutions-coder/sdk`. Routes and components import `getWallowAuthSdk`.
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
  isSafeReturnUrl,
} from "@bc-solutions-coder/sdk";

import { wireCsrfInterceptor } from "./csrf";

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
 * The guarded singleton. Kept at module scope and built lazily: nothing is
 * configured at import time, so merely importing this module in a test or an SSR
 * pass has no side effect on the shared client. Its null-ness is the one guard
 * for all three first-use steps (configure, wire CSRF, create the auth client).
 */
let sdk: WallowAuthSdk | null = null;

/**
 * Return the singleton facade, configuring the same-origin client and the CSRF
 * interceptor on first use. Every later call is a cheap no-op that hands back
 * the same instance.
 */
export function getWallowAuthSdk(): WallowAuthSdk {
  if (sdk !== null) {
    return sdk;
  }

  configureBffClient({ baseUrl: "/" });
  wireCsrfInterceptor(client);

  sdk = {
    auth: createAuthClient(),
    oidc: {
      isSafeReturnUrl,
      buildConnectAuthorizeUrl,
      buildConsentSubmitUrl,
      buildExchangeTicketUrl,
      buildConnectLogoutUrl,
    },
  };

  return sdk;
}
