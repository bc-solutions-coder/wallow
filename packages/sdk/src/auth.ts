/**
 * The browser half of the BFF logout contract, plus the `WallowUser` shape the
 * `/bff/user` endpoint resolves.
 *
 * `logout()` is deliberately the ONLY imperative navigation helper left here:
 * login is a plain link built with `loginRedirect()` (route-context.ts), and the
 * current user is read through `getCurrentUser`/`currentUserQuery`. Logout alone
 * cannot be a link — `/bff/logout` is CSRF-gated and answers 405 to a GET — so
 * the SDK ships the client call matching its own handler, pinned together by
 * `auth-logout.contract.test.ts`.
 */

import { readCsrfCookie } from "./csrf";

/** First status the BFF uses to refuse a request outright (403 CSRF, 405 method). */
const HTTP_FIRST_ERROR: number = 400;

/**
 * Header the BFF's CSRF gate reads. Duplicated from the server entry's
 * `CSRF_HEADER` on purpose: importing `./server/proxy` here would drag the Node
 * BFF into the browser bundle.
 */
const CSRF_HEADER: string = "x-csrf-token";

/**
 * A user identity resolved from the BFF `/bff/user` endpoint.
 *
 * `sub` is always present; other standard claims are optional, and arbitrary
 * additional claims are permitted via the index signature.
 */
export interface WallowUser {
  sub: string;
  email?: string;
  name?: string;
  [claim: string]: unknown;
}

/**
 * Fail with an actionable message when a browser-only navigation helper is
 * called outside the browser.
 *
 * `logout()` navigates by assigning to the global `location`, which Node has
 * no equivalent for; a partially-polyfilled SSR runtime can also expose it as
 * `undefined`. `typeof` covers both shapes. Importing this module stays safe
 * either way — the check lives inside the function bodies, never at module
 * scope — so the browser entry can be pulled into an SSR bundle unchanged.
 *
 * @param caller Name of the calling helper, e.g. `logout()`, used in the message.
 */
function assertBrowserNavigation(caller: string): void {
  const canNavigate: boolean = typeof location !== "undefined";

  if (!canNavigate) {
    throw new Error(
      `${caller} can only run in the browser: it navigates by assigning to the global \`location\`, which is unavailable during server-side rendering. Call it from a client-side event handler or effect.`,
    );
  }
}

/**
 * Options for {@link logout}.
 */
export interface LogoutOptions {
  /**
   * CSRF token to echo in the `x-csrf-token` header. Defaults to the BFF's
   * non-HttpOnly double-submit cookie — the same single source the request
   * interceptor reads.
   */
  csrfToken?: string;
}

/**
 * End the BFF session and send the browser on to the IdP's end-session URL.
 *
 * `/bff/logout` is a state-changing endpoint: it answers `405` to anything but
 * a `POST` carrying a valid `x-csrf-token`, so this cannot be a plain
 * navigation. The session-clearing request is issued with `fetch`, and the
 * browser is then navigated to the end-session URL the handler answers with.
 *
 * @param options Optional {@link LogoutOptions}.
 * @throws Error synchronously when called outside a browser context; rejects
 *         when the BFF refuses the logout.
 */
export function logout(options?: LogoutOptions): Promise<void> {
  // Not an `async function`: the SSR guard must throw synchronously (see
  // Wallow-pu6a.3.6), and `async` would turn that throw into a rejection.
  assertBrowserNavigation("logout()");

  return endSession(options);
}

/**
 * Resolve the CSRF token to present: an explicit option, otherwise the readable
 * double-submit cookie.
 *
 * @returns The token, or `null` when the browser holds none — in which case no
 *          header is sent at all and the BFF stays the authority, answering 403.
 */
function resolveCsrfToken(options?: LogoutOptions): string | null {
  return options?.csrfToken ?? readCsrfCookie();
}

/** Status the handler answers when there was no session to log out of. */
const HTTP_NO_CONTENT: number = 204;

/**
 * The success body of `POST /bff/logout`. The handler answers the IdP
 * end-session URL as JSON rather than a redirect because a redirect's target
 * is unreadable to a `fetch` caller: with `redirect: "manual"` the response is
 * opaqueredirect (status 0, headers filtered — even same-origin), and letting
 * fetch follow it would put the IdP endpoint behind CORS. Only a body survives
 * to the caller, which then makes the hop as a real navigation.
 */
interface LogoutResponseBody {
  logoutUrl?: string;
}

/**
 * Issue the gated `POST /bff/logout` and navigate to the end-session URL the
 * handler answers with.
 *
 * @throws Error when the BFF refuses the logout, leaving the browser where it is.
 */
async function endSession(options?: LogoutOptions): Promise<void> {
  const token: string | null = resolveCsrfToken(options);
  const headers: Record<string, string> = {};

  if (token !== null) {
    headers[CSRF_HEADER] = token;
  }

  const response: Response = await fetch("/bff/logout", {
    method: "POST",
    credentials: "include",
    headers,
  });

  if (response.status >= HTTP_FIRST_ERROR) {
    throw new Error(
      `Logout failed: the BFF answered ${response.status}. The session may still be active.`,
    );
  }

  // 204 is the anonymous no-op: the cookies were cleared but there was no
  // session, so there is no OP session to end either — the app root is home.
  if (response.status === HTTP_NO_CONTENT) {
    location.href = "/";
    return;
  }

  const body: LogoutResponseBody = (await response.json()) as LogoutResponseBody;
  // The IdP hop ends the SSO session (and fans out front-/back-channel logout
  // to other relying parties); without a URL the local session is still gone,
  // so the app root remains the safe landing.
  location.href = body.logoutUrl ?? "/";
}
