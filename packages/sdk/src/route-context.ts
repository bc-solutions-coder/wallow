/**
 * SSR-safe authentication guards with an injected router redirect. Full-document href navigation sends login requests to the BFF outside the application route tree.
 */
import type { WallowUser } from "./auth";

/** The BFF login endpoint. Outside the route tree — see {@link LoginRedirectOptions}. */
const BFF_LOGIN_PATH = "/bff/login";

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

/** Optional hints for {@link loginRedirect}. */
export interface LoginHints {
  /**
   * The organization to sign in to. The BFF forwards it to the authorize
   * request, where the IdP runs that organization's enrollment policy and
   * scopes the new session to it — the silent re-authorize behind an
   * organization picker. Blank input is treated as absent.
   */
  readonly organization?: string;
}

/**
 * Build the redirect target that sends a visitor to the BFF login endpoint and
 * back to where they were heading — an unauthenticated visitor's login, or a
 * signed-in member's switch of organization context.
 *
 * Pure and SSR-safe: it reads no globals and performs no navigation. Hand the
 * result to the router's `redirect()` and throw that, or use its `href` on a
 * full-document link.
 *
 * @param returnTo Path to return to after authenticating. Defaults to `"/"`;
 *                 blank input is treated as absent. It is URL-encoded, so a
 *                 path carrying its own query string survives intact.
 * @param hints    See {@link LoginHints}.
 */
export function loginRedirect(returnTo?: string, hints?: LoginHints): LoginRedirectOptions {
  const target: string = returnTo?.trim() || "/";
  const organization: string = hints?.organization?.trim() ?? "";
  const organizationQuery: string =
    organization === "" ? "" : `&organization=${encodeURIComponent(organization)}`;

  return {
    href: `${BFF_LOGIN_PATH}?returnTo=${encodeURIComponent(target)}${organizationQuery}`,
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
