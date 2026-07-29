/**
 * Sealed cookie session helpers for the BFF tunnel.
 *
 * A {@link BffSession} holds the OIDC tokens and resolved user identity for a
 * signed-in browser session. It is sealed into an opaque, encrypted string via
 * iron-webcrypto before being written to a same-site cookie, and unsealed on
 * each request.
 */

import { defaults, seal, unseal } from "iron-webcrypto";

import { DEFAULT_SESSION_TTL_SECONDS, type CookieSecret } from "./config";
import { sealPassword, unsealPassword } from "./cookie-secret";
import { webCrypto } from "./webcrypto";

const MS_PER_SECOND: number = 1000;

/**
 * Lifetime baked into a sealed session blob when the caller does not pass an
 * explicit one, in milliseconds. Mirrors {@link DEFAULT_SESSION_TTL_SECONDS} so
 * a blob sealed by a caller that never threads config through still expires.
 */
export const DEFAULT_SESSION_TTL_MS: number = DEFAULT_SESSION_TTL_SECONDS * MS_PER_SECOND;

/**
 * The server-side session persisted (sealed) in the BFF session cookie.
 *
 * `accessToken` is always present; refresh and id tokens are optional. `user`
 * carries the resolved identity claims — `sub` is required, other standard
 * claims are optional, and arbitrary additional claims pass through via the
 * index signature.
 */
export interface BffSession {
  /** Stable identifier for this session, used for server-side lookups. */
  sessionId: string;
  accessToken: string;
  refreshToken?: string;
  idToken?: string;
  /** Access-token expiry as epoch milliseconds (NOT Unix seconds). */
  expiresAt: number;
  user: {
    sub: string;
    email?: string;
    name?: string;
    roles?: string[];
    permissions?: string[];
    tenantId?: string;
    tenantName?: string;
    [claim: string]: unknown;
  };
  /** Monotonic session version, bumped on token refresh / rotation. */
  version: number;
  /** Synchronizer CSRF token for double-submit validation. */
  csrfToken?: string;
}

/**
 * Seal a {@link BffSession} into an opaque, encrypted string suitable for
 * storing in a cookie.
 *
 * @param session The session to seal.
 * @param password The cookie password (>= 32 characters), or a keyed
 *        {@link CookieSecret} set, in which case its ACTIVE key seals the blob.
 * @param ttlMs Lifetime baked into the blob, in milliseconds. Defaults to
 *        {@link DEFAULT_SESSION_TTL_MS}.
 * @returns The sealed, URL-safe token string.
 */
export function sealSession(
  session: BffSession,
  password: CookieSecret,
  ttlMs: number = DEFAULT_SESSION_TTL_MS,
): Promise<string> {
  return seal(webCrypto, session, sealPassword(password), { ...defaults, ttl: ttlMs });
}

/**
 * Unseal a previously {@link sealSession sealed} session string.
 *
 * @param sealed The sealed token produced by {@link sealSession}.
 * @param password The cookie password used to seal it, or a keyed
 *        {@link CookieSecret} set — ANY key in the set may have sealed the blob.
 * @param ttlMs Session lifetime in milliseconds. Defaults to
 *        {@link DEFAULT_SESSION_TTL_MS}. The blob's own baked-in expiry always
 *        wins; this value can never extend it.
 * @returns The decoded session, or `null` when the token is invalid, tampered,
 *          expired, or sealed with a password no longer in the set.
 */
export async function unsealSession(
  sealed: string,
  password: CookieSecret,
  ttlMs: number = DEFAULT_SESSION_TTL_MS,
): Promise<BffSession | null> {
  try {
    const result: unknown = await unseal(webCrypto, sealed, unsealPassword(password), {
      ...defaults,
      ttl: ttlMs,
    });
    return result as BffSession;
  } catch {
    return null;
  }
}
