/**
 * Browser auth helpers that talk to the same-origin BFF tunnel.
 */

/** HTTP 401 Unauthorized — the BFF returns this when no session is active. */
const HTTP_UNAUTHORIZED: number = 401;

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
 * Navigate the browser to the BFF login endpoint, preserving where the user
 * should land afterwards.
 *
 * @param returnTo Path to return to after a successful login. Defaults to "/".
 */
export function login(returnTo: string = "/"): void {
  location.href = `/bff/login?returnTo=${encodeURIComponent(returnTo)}`;
}

/**
 * Navigate the browser to the BFF logout endpoint.
 */
export function logout(): void {
  location.href = "/bff/logout";
}

/**
 * Options for {@link getUser}.
 */
export interface GetUserOptions {
  /**
   * Absolute origin (e.g. `http://localhost:3000`) to resolve the `/bff/user`
   * request against. Required during SSR, where the global (Node/undici) `fetch`
   * cannot parse a relative URL and throws `Failed to parse URL from /bff/user`.
   * Omit it in the browser to keep the same-origin relative request.
   */
  baseUrl?: string;
  /**
   * Extra request headers to attach. Used during SSR to forward the incoming
   * session `Cookie` header, since the Node `fetch` has no cookie jar and
   * `credentials: "include"` alone would send an anonymous request. Omit it in
   * the browser, where the cookie rides along automatically.
   */
  headers?: Record<string, string>;
}

/**
 * Fetch the current user from the BFF `/bff/user` endpoint.
 *
 * @param options Optional {@link GetUserOptions}; pass `baseUrl` during SSR so the
 *                request target is an absolute URL the Node fetch can resolve.
 * @returns The parsed user on 200, or `null` when unauthenticated (401).
 *          Throws on any other non-ok response.
 */
export async function getUser(options?: GetUserOptions): Promise<WallowUser | null> {
  const target: string = options?.baseUrl ? `${options.baseUrl}/bff/user` : "/bff/user";

  const init: RequestInit = { credentials: "include" };
  if (options?.headers) {
    init.headers = options.headers;
  }

  const response: Response = await fetch(target, init);

  if (response.status === HTTP_UNAUTHORIZED) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch user: ${response.status}`);
  }

  return (await response.json()) as WallowUser;
}
