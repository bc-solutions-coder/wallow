/**
 * Server-side session storage abstractions for the BFF tunnel.
 *
 * {@link SessionStore} decouples the BFF handlers from where session state
 * lives. The default cookie-only store seals the whole {@link BffSession} into
 * the session cookie, while server-side stores (e.g. Redis) persist the session
 * out of band and keep only an opaque reference in the cookie.
 */

import type { BffSession } from "../session";

/**
 * Minimal Redis-compatible client surface used by server-side session stores.
 *
 * Only the handful of operations the store needs are declared, so any client
 * (node-redis, ioredis, Upstash, etc.) can be adapted without pulling in a
 * concrete dependency.
 */
export interface RedisLike {
  /**
   * Get the string value at `key`, or `null` when the key does not exist.
   */
  get: (key: string) => Promise<string | null>;
  /**
   * Set `key` to `value`.
   *
   * @param opts Optional flags: `ex` sets an expiry in seconds; `nx` only sets
   *             the key when it does not already exist.
   * @returns `"OK"` on success, or `null` when a conditional set (`nx`) was
   *          skipped because the key already exists.
   */
  set: (key: string, value: string, opts?: { ex?: number; nx?: boolean }) => Promise<"OK" | null>;
  /**
   * Delete `key`, returning the number of keys removed.
   */
  del: (key: string) => Promise<number>;
}

/**
 * Persists and retrieves {@link BffSession} state on behalf of the BFF handlers.
 *
 * Implementations decide how a session is stored and referenced. `write`
 * returns the opaque reference the caller should place in the session cookie;
 * `read` resolves that reference back into a session.
 */
export interface SessionStore {
  /**
   * Resolve a session by its opaque reference, or `null` when it is missing,
   * expired, or invalid.
   */
  read: (ref: string) => Promise<BffSession | null>;
  /**
   * Persist a session, returning the opaque reference to store in the cookie.
   */
  write: (session: BffSession) => Promise<string>;
  /**
   * Remove the session identified by `ref`.
   */
  destroy: (ref: string) => Promise<void>;
  /**
   * Run `fn` while holding a refresh lock for `ref`, serializing concurrent
   * token refreshes for the same session.
   *
   * @returns The result of `fn`, or `undefined` when the lock could not be
   *          acquired (another refresh is already in progress).
   */
  withRefreshLock: <T>(ref: string, fn: () => Promise<T>) => Promise<T | undefined>;
}
