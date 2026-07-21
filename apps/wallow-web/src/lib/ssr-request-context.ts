/**
 * SSR request-context indirection (Wallow-cqoa).
 *
 * During a full-page load wallow-web renders on the server, where any BFF-bound
 * `fetch` (the SDK's `getUser()` and the generated `/api/**` client) needs two
 * things the browser supplies for free: an ABSOLUTE origin (Node's `fetch`
 * cannot parse a relative URL) and the incoming session `Cookie` (Node has no
 * cookie jar, so `credentials: "include"` sends an anonymous request). Both are
 * request-scoped, so they must be resolved per render rather than configured
 * once.
 *
 * This module is the browser-safe seam between the SSR entry — which owns the
 * request and the `node:async_hooks` `AsyncLocalStorage` that scopes it (see
 * `src/ssr.tsx`) — and `getWallowSdk()`, which reads the context when
 * `import.meta.env.SSR` is true. Keeping the `AsyncLocalStorage` out of here
 * (only a resolver reference lives at module scope) means this file carries NO
 * node import, so it bundles cleanly into the browser build, where the resolver
 * is simply never registered and {@link getSsrRequestContext} returns
 * `undefined`.
 */

/** The per-request facts an SSR-time BFF `fetch` needs. */
export interface SsrRequestContext {
  /** Absolute origin of the incoming request, e.g. `http://localhost:3000`. */
  origin: string;
  /** The incoming `Cookie` header (carries the `wallow_bff` session), if any. */
  cookie: string | undefined;
}

let resolver: (() => SsrRequestContext | undefined) | undefined;

/**
 * Register the resolver that reads the current request's context. Called once by
 * the SSR entry (`src/ssr.tsx`) with an `AsyncLocalStorage`-backed getter. Never
 * called in the browser bundle.
 */
export function setSsrRequestContextResolver(next: () => SsrRequestContext | undefined): void {
  resolver = next;
}

/**
 * Read the context for the in-flight SSR render, or `undefined` when there is no
 * active request (browser, or a call outside a `render()` scope).
 */
export function getSsrRequestContext(): SsrRequestContext | undefined {
  return resolver?.();
}
