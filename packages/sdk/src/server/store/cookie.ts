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
 * Stores the entire {@link BffSession} inside the session cookie.
 *
 * The reference returned by {@link write} is the sealed session string, which
 * {@link read} unseals back into a session. There is no out-of-band state, so
 * {@link destroy} is a no-op (cookie clearing is the module's job), while
 * {@link withRefreshLock} coalesces concurrent refreshes for one reference onto
 * a single in-memory promise.
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

  constructor(options: CookieSessionStoreOptions) {
    this.password = options.password;
    this.ttlMs = (options.ttlSeconds ?? DEFAULT_SESSION_TTL_SECONDS) * MS_PER_SECOND;
  }

  read(ref: string): Promise<BffSession | null> {
    return unsealSession(ref, this.password, this.ttlMs);
  }

  write(session: BffSession): Promise<string> {
    return sealSession(session, this.password, this.ttlMs);
  }

  destroy(_ref: string): Promise<void> {
    return Promise.resolve();
  }

  /**
   * Run `fn` as the refresh for `ref`, or join the refresh already in flight.
   *
   * This COALESCES rather than merely serializing: while a refresh for `ref` is
   * outstanding, a second caller does not run its own callback at all — it
   * receives the first call's promise and observes that result (or failure).
   * Serializing alone would not fix the double-spend this guards against,
   * because `refreshUnderLock` in `proxy.ts` reads `session.refreshToken` before
   * taking the lock, so a callback that merely runs *later* still presents the
   * one-time token the first callback just spent. Exactly one exchange must
   * happen, and every caller must adopt its outcome.
   *
   * For this store `ref` is the sealed cookie string itself, so concurrent
   * requests carrying the same browser session present an identical `ref` and
   * coalesce, while distinct sessions never interact.
   *
   * LIMITATION — this mutex is per-PROCESS. It makes no guarantee across
   * instances, so a multi-instance deployment still needs the Valkey-backed
   * `ValkeySessionStore` for cross-process refresh safety. That tradeoff is
   * inherent to a cookie-only store (there is no shared substrate to lock on)
   * and is unchanged by the coalescing added here.
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
