/**
 * Lazy one-time client configuration for the SDK query layer.
 *
 * The queryOptions factories in this directory call generated ops on the shared
 * `@hey-api` client, but HOW that client is configured is app-specific
 * (wallow-web: BFF tunnel under `/api` + SSR request context; wallow-auth:
 * same-origin passthrough at `/`). Apps register their configurator once at
 * module scope (side-effect free); the first queryFn to run invokes it. This
 * preserves the `createConfiguredOnce` lazy semantics — nothing touches the
 * shared client until a query actually fires, which is required for SSR where
 * the request context only exists inside the per-request ALS scope.
 */
let configurator: (() => void) | undefined;
let bootstrapped = false;

/** Register the app's one-time client configurator. Re-registering re-arms the guard. */
export function registerQueryBootstrap(configure: () => void): void {
  configurator = configure;
  bootstrapped = false;
}

/** Run the registered configurator if it has not run yet. Safe to call on every queryFn. */
export function ensureQueryBootstrapped(): void {
  if (!bootstrapped) {
    configurator?.();
    bootstrapped = true;
  }
}

/** Test-only: clear registration state. */
export function resetQueryBootstrapForTests(): void {
  configurator = undefined;
  bootstrapped = false;
}
