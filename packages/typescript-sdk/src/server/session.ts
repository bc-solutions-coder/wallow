/**
 * Sealed cookie session helpers for the BFF tunnel.
 *
 * A {@link BffSession} holds the OIDC tokens and resolved user identity for a
 * signed-in browser session. It is sealed into an opaque, encrypted string via
 * iron-webcrypto before being written to a same-site cookie, and unsealed on
 * each request.
 */

import { defaults, seal, unseal } from "iron-webcrypto";

import { webCrypto } from "./webcrypto";

/**
 * The server-side session persisted (sealed) in the BFF session cookie.
 *
 * `accessToken` is always present; refresh and id tokens are optional. `user`
 * carries the resolved identity claims — `sub` is required, other standard
 * claims are optional, and arbitrary additional claims pass through via the
 * index signature.
 */
export interface BffSession {
  accessToken: string;
  refreshToken?: string;
  idToken?: string;
  /** Access-token expiry as epoch milliseconds. */
  expiresAt: number;
  user: {
    sub: string;
    email?: string;
    name?: string;
    [claim: string]: unknown;
  };
}

/**
 * Seal a {@link BffSession} into an opaque, encrypted string suitable for
 * storing in a cookie.
 *
 * @param session The session to seal.
 * @param password The cookie password (>= 32 characters).
 * @returns The sealed, URL-safe token string.
 */
export function sealSession(
  session: BffSession,
  password: string,
): Promise<string> {
  return seal(webCrypto, session, password, defaults);
}

/**
 * Unseal a previously {@link sealSession sealed} session string.
 *
 * @param sealed The sealed token produced by {@link sealSession}.
 * @param password The cookie password used to seal it.
 * @returns The decoded session, or `null` when the token is invalid, tampered,
 *          or sealed with a different password.
 */
export async function unsealSession(
  sealed: string,
  password: string,
): Promise<BffSession | null> {
  try {
    const result: unknown = await unseal(webCrypto, sealed, password, defaults);
    return result as BffSession;
  } catch {
    return null;
  }
}
