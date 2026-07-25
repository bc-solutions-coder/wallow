/**
 * `getSdk()` facade — the guarded-singleton entry every route/component would use
 * to reach the API. It configures the shared `@hey-api` client for same-origin
 * use and wires the CSRF request interceptor exactly once, then hands back a
 * namespaced object.
 *
 * SAME-ORIGIN BASE URL: this app's h3 host is a passthrough reverse proxy that
 * forwards `/v1/**` and `/connect/**` verbatim at the ROOT (see
 * `src/lib/proxy-server.ts`), so the client's baseUrl is `/`, not `/api`.
 * `configureBffClient` does `setConfig({ baseUrl, credentials: "include" })`, and
 * `credentials: "include"` is what carries the auth cookie.
 *
 * CONVENTION: this file is the only place in the app allowed to import from
 * `@bc-solutions-coder/sdk`. Routes and components import `getSdk`.
 *
 * The minimal reference screen (`HelloCard`) renders no live data, so nothing
 * calls this at boot; it exists to show the SDK wiring a real fork would use.
 */
import {
  type AuthClient,
  client,
  configureBffClient,
  createAuthClient,
  createConfiguredOnce,
  wireCsrfInterceptor,
} from "@bc-solutions-coder/sdk";

/** The namespaced facade the singleton hands back. */
export interface Sdk {
  /** Typed identity endpoints (SDK `createAuthClient()`). */
  auth: AuthClient;
}

/**
 * Return the singleton facade, configuring the same-origin client and the CSRF
 * interceptor on first use. Every later call hands back the same instance. The
 * one-time configure-then-build guard is the SDK's shared
 * {@link createConfiguredOnce} helper — built lazily, so merely importing this
 * module (in a test or SSR pass) has no side effect on the shared client.
 */
export const getSdk: () => Sdk = createConfiguredOnce(
  () => {
    configureBffClient({ baseUrl: "/" });
    wireCsrfInterceptor(client);
  },
  (): Sdk => ({
    auth: createAuthClient(),
  }),
);
