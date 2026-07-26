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
 * `@bc-solutions-coder/sdk`. Routes and components import `getSdk` for writes and
 * reach reads through the SDK's `./query` factories, which this module bootstraps.
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
import { ensureQueryBootstrapped, registerQueryBootstrap } from "@bc-solutions-coder/sdk/query";

/** The namespaced facade the singleton hands back. */
export interface Sdk {
  /** Typed identity endpoints (SDK `createAuthClient()`). */
  auth: AuthClient;
}

/**
 * Point the shared `@hey-api` client at the same-origin root and wire the CSRF
 * request interceptor. Runs exactly once per module graph — see
 * {@link registerQueryBootstrap} below. There is NO SSR branch: this app's h3
 * server is a passthrough proxy, so `/` is the correct base URL in both passes.
 */
export function configureClient(): void {
  configureBffClient({ baseUrl: "/" });
  wireCsrfInterceptor(client);
}

// Register the configurator with the SDK query layer so the `./query` factories
// configure the client lazily on the first query. This app fires none of its own,
// but a fork's first `useQuery(xQueries.y())` needs the client already pointed at
// the right origin — registering here is what makes that work with no extra
// wiring. Registration is side-effect free: nothing touches the client until a
// query (or the facade) actually runs.
registerQueryBootstrap(configureClient);

/**
 * Return the singleton facade, configuring the same-origin client and the CSRF
 * interceptor on first use. Every later call hands back the same instance. The
 * one-time configure-then-build guard is the SDK's shared
 * {@link createConfiguredOnce} helper — built lazily, so merely importing this
 * module (in a test or SSR pass) has no side effect on the shared client.
 *
 * The configure step DELEGATES to {@link ensureQueryBootstrapped} rather than
 * calling {@link configureClient} directly: the facade's guard and the query
 * bootstrap's guard are independent, so calling the configurator from both would
 * run it twice in whichever order the app hits them, and the second pass would
 * register a SECOND CSRF interceptor on the shared client.
 */
export const getSdk: () => Sdk = createConfiguredOnce(
  ensureQueryBootstrapped,
  (): Sdk => ({
    auth: createAuthClient(),
  }),
);
