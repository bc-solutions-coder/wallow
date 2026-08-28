/**
 * Typed router context + SSR-safe auth guard (Wallow-pu6a.5.6).
 *
 * Route files used to hand-type `context.sdk`/`context.queryClient` ad hoc and
 * hand-roll the unauthenticated redirect inline. {@link WallowRouterContext} is
 * that context shape declared once, and {@link requireAuth}/{@link loginRedirect}
 * are the guard extracted from wallow-web's `/dashboard` route.
 *
 * SSR SAFETY IS THE WHOLE POINT (Wallow-zyxe). A guard runs in `beforeLoad`,
 * which executes during a full-page server render as well as in the browser, so
 * it may never navigate by assigning to the global `location` — Node has none,
 * and the SDK's since-deleted browser-only `login()` helper turned a gated SSR
 * load into an HTTP 500 for exactly that reason. Nothing in this module reads `location`,
 * `document`, or `window`; it only BUILDS a redirect target and hands it to the
 * router's own `redirect()`, which the SSR request handler turns into a 307 and
 * the client router turns into a navigation.
 *
 * Two properties are structural rather than incidental:
 *
 *   - the target is an `href`, never a `to`. `/bff/login` is a BFF endpoint, not
 *     a route in the app's route tree, so a `to` (or a relative `href` without
 *     `reloadDocument`) is committed against the route tree and lands on a
 *     not-found match instead of reaching the BFF.
 *   - `reloadDocument` is baked in by {@link loginRedirect} rather than left to
 *     each call site, because forgetting it is the same bug in a quieter form.
 *
 * The router itself is NOT imported here. `redirect` is injected by the caller,
 * so the SDK gains no dependency on `@tanstack/react-router` and the guard stays
 * unit-testable without a router.
 */
import type { QueryClient } from "@tanstack/react-query";

import type { WallowUser } from "./auth";
import type { WallowSdk } from "./create-sdk";

/** The BFF login endpoint. Outside the route tree — see {@link LoginRedirectOptions}. */
const BFF_LOGIN_PATH = "/bff/login";

/**
 * The shape apps put in their router context, so route files get a typed
 * `context` instead of re-declaring it.
 *
 * `queryClient` and `sdk` are both request-scoped: a router built per request
 * owns one `QueryClient` (never a module-global one, which would hand one
 * user's cache to the next) and the request's own SDK instance.
 */
export interface WallowRouterContext {
  /** The request's TanStack Query cache, shared by loaders and components. */
  readonly queryClient: QueryClient;
  /** The request's SDK instance; pass `sdk.client` to generated operations. */
  readonly sdk: WallowSdk;
  /**
   * The resolved current user, when an app chooses to put it in context.
   * `null` means "resolved, and unauthenticated"; absent means "not resolved
   * here".
   */
  readonly user?: WallowUser | null;
}

/**
 * A redirect target for the BFF login endpoint: everything the router's
 * `redirect()` needs, and nothing a caller can get subtly wrong.
 */
export interface LoginRedirectOptions {
  /** Absolute path to the BFF login endpoint, with the encoded `returnTo`. */
  readonly href: string;
  /**
   * Always `true`. `/bff/login` lives outside the route tree, so the navigation
   * must leave the client router rather than be committed through it.
   */
  readonly reloadDocument: true;
}

/**
 * Build the redirect target that sends an unauthenticated visitor to the BFF
 * login endpoint and back to where they were heading.
 *
 * Pure and SSR-safe: it reads no globals and performs no navigation. Hand the
 * result to the router's `redirect()` and throw that.
 *
 * @param returnTo Path to return to after authenticating. Defaults to `"/"`;
 *                 blank input is treated as absent. It is URL-encoded, so a
 *                 path carrying its own query string survives intact.
 */
export function loginRedirect(returnTo?: string): LoginRedirectOptions {
  const target: string = returnTo?.trim() || "/";

  return {
    href: `${BFF_LOGIN_PATH}?returnTo=${encodeURIComponent(target)}`,
    reloadDocument: true,
  };
}

/** Options for {@link requireAuth}. */
export interface RequireAuthOptions<TUser extends WallowUser, TRedirect> {
  /**
   * The resolved current user — typically
   * `await context.queryClient.ensureQueryData(...)` — or `null`/`undefined`
   * when there is no session.
   */
  readonly user: TUser | null | undefined;
  /** Where to send the visitor back to after login; see {@link loginRedirect}. */
  readonly returnTo?: string;
  /**
   * The router's `redirect()`, injected so this module needs no router
   * dependency. Called with {@link loginRedirect}'s output and its result is
   * thrown.
   */
  readonly redirect: (options: LoginRedirectOptions) => TRedirect;
}

/**
 * Gate a route on an authenticated user: return the user when there is one,
 * otherwise throw the injected router redirect to the BFF login.
 *
 * The return type narrows away `null`/`undefined`, so a `beforeLoad` can use the
 * result directly without a non-null assertion.
 *
 * @param options See {@link RequireAuthOptions}.
 * @returns The authenticated user.
 * @throws Whatever `options.redirect` returns, when there is no user.
 */
export function requireAuth<TUser extends WallowUser, TRedirect>(
  options: RequireAuthOptions<TUser, TRedirect>,
): TUser {
  if (options.user === null || options.user === undefined) {
    throw options.redirect(loginRedirect(options.returnTo));
  }

  return options.user;
}
