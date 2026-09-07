/**
 * Cookie-only {@link SessionStore} implementation.
 *
 * The cookie itself holds all session state: the opaque reference returned by
 * {@link CookieSessionStore.write} is the sealed {@link BffSession} string, so
 * there is no server-side state to persist or destroy.
 *
 * The store does keep one piece of ephemeral, in-memory state: the refreshes
 * currently in flight, so that concurrent callers coalesce onto a single token
 * exchange rather than each spending the same one-time refresh token. See
 * {@link CookieSessionStore.withRefreshLock} for the semantics and its
 * single-process limitation.
 */

import { DEFAULT_SESSION_TTL_SECONDS, type CookieSecret } from "../config";
import { type BffSession, sealSession, unsealSession } from "../session";
import { type SessionStore } from "./types";

const MS_PER_SECOND: number = 1000;

/**
 * Options for {@link CookieSessionStore}.
 */
export interface CookieSessionStoreOptions {
  /**
   * The cookie password used to seal and unseal sessions (>= 32 characters), or
   * a keyed set during a rotation — every key in it can unseal, and its active
   * key seals.
   */
  password: CookieSecret;
  /**
   * Lifetime baked into the sealed blob, in seconds, so a sealed session cannot
   * outlive its session TTL. Defaults to `DEFAULT_SESSION_TTL_SECONDS`.
   */
  ttlSeconds?: number;
}

/**
 * Store a complete BFF session in a sealed browser-cookie reference.
 *
 * No server record exists, so destroy cannot invalidate copied cookies and back-channel
 * revocation is unavailable. Refresh calls sharing the same reference join one promise within
 * this store instance. Use ValkeySessionStore when sessions must be revoked server-side or
 * refresh coordination must span replicas.
 */
export class CookieSessionStore implements SessionStore {
  private readonly password: CookieSecret;
  private readonly ttlMs: number;
  /**
   * Refreshes currently in flight, keyed by the `ref` they were started for.
   *
   * Entries are removed as soon as the refresh settles, so the map only ever
   * holds genuinely concurrent refreshes and cannot grow unbounded. The value is
   * `Promise<unknown>` because each call supplies its own `T`; the read side
   * restores that `T` (see {@link withRefreshLock}).
   */
  private readonly refreshLocks = new Map<string, Promise<unknown>>();

  /**
   * Create a cookie store with the sealing secret and optional session TTL in seconds.
   * Defaults to a one-day TTL. Construction does not read or write browser cookies.
   */
  constructor(options: CookieSessionStoreOptions) {
    this.password = options.password;
    this.ttlMs = (options.ttlSeconds ?? DEFAULT_SESSION_TTL_SECONDS) * MS_PER_SECOND;
  }

  /**
   * Unseal a cookie reference into its session, or return null for an invalid or expired
   * seal. Does not refresh an expired access token.
   */
  read(ref: string): Promise<BffSession | null> {
    return unsealSession(ref, this.password, this.ttlMs);
  }

  /**
   * Seal the full session with the configured password and TTL, returning the cookie
   * reference. The caller must write the reference to response cookies.
   */
  write(session: BffSession): Promise<string> {
    return sealSession(session, this.password, this.ttlMs);
  }

  /**
   * Resolve without changing state. Cookie-only storage has no server record to revoke; the
   * caller clears browser cookies separately.
   */
  destroy(_ref: string): Promise<void> {
    return Promise.resolve();
  }

  /**
   * Run fn once for concurrent refreshes sharing this cookie reference and store instance.
   * Other callers join the same promise and receive its result or rejection. Coordination
   * does not span store instances or server replicas.
   */
  withRefreshLock<T>(ref: string, fn: () => Promise<T>): Promise<T | undefined> {
    const inFlight: Promise<unknown> | undefined = this.refreshLocks.get(ref);
    if (inFlight !== undefined) {
      // Join the outstanding refresh — deliberately without calling `fn`. The
      // cast restores the `T` erased on the way into the map; it is sound
      // because callers refreshing the same session return the same shape, and
      // a joined caller is asking for precisely that in-flight result.
      return inFlight as Promise<T>;
    }

    const run: Promise<T> = fn();
    this.refreshLocks.set(ref, run);

    // Release on settle, success or failure alike: a rejected refresh frees the
    // ref for a later attempt instead of poisoning it. The guard keeps a slow
    // failure from evicting a newer entry that already replaced this one.
    const release = (): void => {
      if (this.refreshLocks.get(ref) === run) {
        this.refreshLocks.delete(ref);
      }
    };
    // Handlers are attached here, and never returned to callers, so `run` always
    // has a rejection handler (no unhandled rejection) while every caller still
    // receives `run` itself rather than a derived promise.
    void run.then(release, release);

    return run;
  }
}
