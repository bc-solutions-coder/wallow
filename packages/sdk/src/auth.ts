/**
 * Browser auth helpers that talk to the same-origin BFF tunnel.
 */

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
 * Fetch the current user from the BFF `/bff/user` endpoint.
 *
 * @returns The parsed user on 200, or `null` when unauthenticated (401).
 *          Throws on any other non-ok response.
 */
export async function getUser(): Promise<WallowUser | null> {
  const response: Response = await fetch("/bff/user", {
    credentials: "include",
  });

  if (response.status === 401) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch user: ${response.status}`);
  }

  return (await response.json()) as WallowUser;
}
