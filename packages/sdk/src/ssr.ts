/**
 * SSR request-context seam + SSR-time BFF client configuration (Wallow-0q2s.7.2).
 *
 * During a full-page load a same-origin BFF frontend renders on the server, where
 * any BFF-bound `fetch` (the SDK's `getUser()` and the generated `/api/**`
 * client) needs two things the browser supplies for free: an ABSOLUTE origin
 * (Node's `fetch` cannot parse a relative URL) and the incoming session `Cookie`
 * (Node has no cookie jar, so `credentials: "include"` sends an anonymous
 * request). Both are request-scoped, so they must be resolved per render rather
 * than configured once.
 *
 * This module is the browser-SAFE seam between the app's SSR entry — which owns
 * the request and the `node:async_hooks` `AsyncLocalStorage` that scopes it — and
 * the isomorphic SDK facade, which reads the context during SSR. Keeping the
 * `AsyncLocalStorage` in the app (only a resolver reference lives at module scope
 * here) means this file carries NO node import, so it bundles cleanly into the
 * browser build, where the resolver is simply never registered and
 * {@link getSsrRequestContext} returns `undefined`. That is why these symbols
 * ship on the browser entry (`.`) and NOT the node-only `./server` subpath.
 */
import { client, configureBffClient } from "./client";
import type { CsrfInterceptorClient } from "./csrf";

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
 * the app's SSR entry with an `AsyncLocalStorage`-backed getter. Never called in
 * the browser bundle.
 */
export function setSsrRequestContextResolver(next: () => SsrRequestContext | undefined): void {
  resolver = next;
}

/**
 * Read the context for the in-flight SSR render, or `undefined` when there is no
 * active request (browser, or a call outside a render scope).
 */
export function getSsrRequestContext(): SsrRequestContext | undefined {
  return resolver?.();
}

/**
 * Wire the SSR cookie-forwarding interceptor onto the given client.
 *
 * During SSR the generated `/api/**` client runs on the server, where Node's
 * `fetch` has no cookie jar, so `credentials: "include"` sends an anonymous
 * request and every authenticated loader 401s. The interceptor reads the
 * in-flight request's `Cookie` (the `wallow_bff` session) from the SSR request
 * context LIVE per request and stamps it, so the BFF proxy resolves the session
 * and attaches the bearer. CSRF is intentionally NOT wired for SSR: only safe GET
 * loaders run there, and the CSRF interceptor stamps nothing on safe methods.
 */
export function wireSsrCookieInterceptor(target: CsrfInterceptorClient): void {
  target.interceptors.request.use((request: Request): Request => {
    const context: SsrRequestContext | undefined = getSsrRequestContext();
    if (context?.cookie !== undefined) {
      request.headers.set("cookie", context.cookie);
    }
    return request;
  });
}

/**
 * Configure the shared BFF client for SSR-time use and wire the cookie-forwarding
 * interceptor onto it.
 *
 * When a request context is supplied the client is pointed at the request's
 * ABSOLUTE origin (`${origin}/api`) so Node's `fetch` can parse the URL; with no
 * context it falls back to the same-origin relative `/api` default. Either way
 * the cookie-forwarding interceptor is wired so the per-request session `Cookie`
 * (read live from the SSR request context) rides along. The origin is stable per
 * host, so configuring it once on the first request is correct; the cookie is NOT
 * captured here — it is read live per request by the interceptor.
 */
export function configureSsrClient(context?: SsrRequestContext): void {
  configureBffClient(context ? { baseUrl: `${context.origin}/api` } : {});
  wireSsrCookieInterceptor(client);
}
