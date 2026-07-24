/**
 * wallow-web's BFF client configurator.
 *
 * As of Wallow-evd5.2.3 the last facade slice (the current-user `user.me()`
 * plumbing) has retired: the current user is now a cached TanStack Query
 * (`userQueries.currentUser()`, `@bc-solutions-coder/sdk/query`) read in the route
 * `beforeLoad`s, and the per-feature data slices moved into the SDK query layer in
 * Wallow-evd5.2.2. What remains here is `configureClient()` — the one-time
 * SSR/browser client-config authority — plus its module-scope registration with
 * the SDK query bootstrap so the shared `@hey-api` client is configured lazily on
 * the first query.
 */
import {
  client,
  configureBffClient,
  configureSsrClient,
  getSsrRequestContext,
  wireCsrfInterceptor,
} from "@bc-solutions-coder/sdk";
import { registerQueryBootstrap } from "@bc-solutions-coder/sdk/query";

/**
 * Configure the BFF client and wire the matching request interceptor.
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
