/**
 * Cookie-only {@link SessionStore} implementation.
 *
 * The cookie itself holds all session state: the opaque reference returned by
 * {@link CookieSessionStore.write} is the sealed {@link BffSession} string, so
 * there is no server-side state to persist, destroy, or lock.
 */

import { type BffSession, sealSession, unsealSession } from "../session";
import { type SessionStore } from "./types";

/**
 * Options for {@link CookieSessionStore}.
 */
export interface CookieSessionStoreOptions {
  /** The cookie password used to seal and unseal sessions (>= 32 characters). */
  password: string;
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

  constructor(options: CookieSessionStoreOptions) {
    this.password = options.password;
  }

  read(ref: string): Promise<BffSession | null> {
    return unsealSession(ref, this.password);
  }

  write(session: BffSession): Promise<string> {
    return sealSession(session, this.password);
  }

  destroy(_ref: string): Promise<void> {
    return Promise.resolve();
  }

  withRefreshLock<T>(_ref: string, fn: () => Promise<T>): Promise<T | undefined> {
    return fn();
  }
}
