/**
 * Cookie-only {@link SessionStore} implementation.
 *
 * The cookie itself holds all session state: the opaque reference returned by
 * {@link CookieSessionStore.write} is the sealed {@link BffSession} string, so
 * there is no server-side state to persist, destroy, or lock.
 */

import { DEFAULT_SESSION_TTL_SECONDS } from "../config";
import { type BffSession, sealSession, unsealSession } from "../session";
import { type SessionStore } from "./types";

const MS_PER_SECOND: number = 1000;

/**
 * Options for {@link CookieSessionStore}.
 */
export interface CookieSessionStoreOptions {
  /** The cookie password used to seal and unseal sessions (>= 32 characters). */
  password: string;
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
 * {@link destroy} is a no-op (cookie clearing is the module's job) and
 * {@link withRefreshLock} runs its callback directly without any locking
 * substrate.
 */
export class CookieSessionStore implements SessionStore {
  private readonly password: string;
  private readonly ttlMs: number;

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

  withRefreshLock<T>(_ref: string, fn: () => Promise<T>): Promise<T | undefined> {
    return fn();
  }
}
