/**
 * getWallowSdk() facade — the guarded-singleton entry that configures the BFF
 * client exactly once and exposes the current-user slice.
 *
 * As of Wallow-evd5.2.2 the per-feature data slices (organizations, apps,
 * settings, mfa, inquiries) have moved into the SDK query layer
 * (`@bc-solutions-coder/sdk/query`); each feature's `api.ts` now re-exports those
 * factories directly, so this facade no longer hand-rolls them. Only the
 * current-user slice remains here (retired in a later task); `configureClient`
 * still owns one-time client configuration and is registered with the SDK query
 * bootstrap so the shared `@hey-api` client is configured lazily on the first
 * query.
 *
 * On first use it configures the BFF client exactly once and wires the CSRF
 * request interceptor onto the shared `@hey-api` client; thereafter it returns a
 * namespaced object whose `user.me()` delegates to the SDK's `getUser()`.
 */
import {
  client,
  configureBffClient,
  configureSsrClient,
  createConfiguredOnce,
  getSsrRequestContext,
  getUser,
  wireCsrfInterceptor,
  type SsrRequestContext,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
import { registerQueryBootstrap } from "@bc-solutions-coder/sdk/query";

/**
 * Configure the BFF client and wire the matching request interceptor. Invoked
 * exactly once by the {@link createConfiguredOnce} guard wrapping
 * `getWallowSdk()`.
 *
 * During SSR (`import.meta.env.SSR`) the SDK's {@link configureSsrClient} points
 * the client at the request's ABSOLUTE origin (`${origin}/api`) so Node's `fetch`
 * can parse the URL and wires the live cookie-forwarding interceptor that carries
 * the session; in the browser the same-origin relative `/api` default and the
 * CSRF interceptor apply. The origin is stable per host, so configuring it once
 * on the first request is correct.
 */
export function configureClient(): void {
  if (import.meta.env.SSR) {
    configureSsrClient(getSsrRequestContext());
  } else {
    configureBffClient();
    wireCsrfInterceptor(client);
  }
}

// Register wallow-web's client configurator with the SDK query layer so its
// `./query` factories configure the shared `@hey-api` client lazily on the first
// query. Registration is side-effect free — nothing touches the client until a
// query actually runs.
registerQueryBootstrap(configureClient);

/** Current-user slice (delegates to the SDK's `getUser()`). */
export interface UserSlice {
  me: () => Promise<WallowUser | null>;
}

/** The namespaced facade object. Only the current-user slice remains. */
export interface WallowSdk {
  user: UserSlice;
}

const sdk: WallowSdk = {
  user: {
    me: () => {
      // During SSR `getUser()` runs under Node's fetch: pass the request's
      // absolute origin (so the URL parses) and forward the session cookie (so
      // the BFF resolves the signed-in user instead of 401ing). In the browser
      // the relative same-origin request with the ambient cookie is correct.
      if (import.meta.env.SSR) {
        const context: SsrRequestContext | undefined = getSsrRequestContext();
        if (context !== undefined) {
          return getUser({
            baseUrl: context.origin,
            ...(context.cookie !== undefined ? { headers: { cookie: context.cookie } } : {}),
          });
        }
      }
      return getUser();
    },
  },
};

/**
 * Return the singleton facade, configuring the BFF client and matching request
 * interceptor on first use. The one-time configure-then-build guard is the SDK's
 * shared {@link createConfiguredOnce} helper.
 */
export const getWallowSdk: () => WallowSdk = createConfiguredOnce(configureClient, () => sdk);
