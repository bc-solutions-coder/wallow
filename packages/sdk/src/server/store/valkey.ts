/**
 * Valkey/Redis-backed {@link SessionStore} implementation.
 *
 * Unlike the cookie-only store, this persists the full {@link BffSession} out of
 * band in a Redis-compatible server (Valkey, Redis, etc.) and keeps only an
 * opaque, sealed reference in the cookie. Because the session lives server-side,
 * {@link ValkeySessionStore.destroy} truly revokes it, and
 * {@link ValkeySessionStore.withRefreshLock} can serialize concurrent token
 * refreshes across processes via a short-lived lock key.
 */

import { defaults, seal, unseal } from "iron-webcrypto";

import { randomUrlSafe } from "../pkce";
import { type BffSession } from "../session";
import { webCrypto } from "../webcrypto";
import { type RedisLike, type SessionStore } from "./types";

/** Default session record lifetime when none is supplied: one day. */
const DEFAULT_TTL_SECONDS = 86_400;

/** Default refresh-lock lifetime when none is supplied (seconds). */
const DEFAULT_LOCK_TTL_SECONDS = 10;

/** Random-byte count for a generated session id. */
const SESSION_ID_BYTES = 24;

/**
 * Options for {@link ValkeySessionStore}.
 */
export interface ValkeySessionStoreOptions {
  /** The Redis-compatible client used to persist sessions and locks. */
  client: RedisLike;
  /** The cookie password used to seal and unseal session references (>= 32 chars). */
  password: string;
  /** Session record time-to-live in seconds. Defaults to `86400` (one day). */
  ttlSeconds?: number;
  /** Refresh-lock time-to-live in seconds. Defaults to `10`. */
  lockTtlSeconds?: number;
  /** Namespace prefix for all keys. Defaults to `"wallow"`. */
  keyPrefix?: string;
}

/**
 * Persists {@link BffSession} state in a Redis-compatible server and references
 * it from the cookie via an opaque sealed session id.
 *
 * The reference stored in the cookie is the session id sealed with
 * iron-webcrypto — it leaks no user data and cannot be forged without the
 * cookie password. Server records are namespaced under
 * `<prefix>:session:<id>`; refresh locks under `<prefix>:refreshlock:<id>`.
 */
export class ValkeySessionStore implements SessionStore {
  private readonly client: RedisLike;
  private readonly password: string;
  private readonly ttlSeconds: number;
  private readonly lockTtlSeconds: number;
  private readonly keyPrefix: string;

  constructor(options: ValkeySessionStoreOptions) {
    this.client = options.client;
    this.password = options.password;
    this.ttlSeconds = options.ttlSeconds ?? DEFAULT_TTL_SECONDS;
    this.lockTtlSeconds = options.lockTtlSeconds ?? DEFAULT_LOCK_TTL_SECONDS;
    this.keyPrefix = options.keyPrefix ?? "wallow";
  }

  private sessionKey(id: string): string {
    return `${this.keyPrefix}:session:${id}`;
  }

  private lockKey(id: string): string {
    return `${this.keyPrefix}:refreshlock:${id}`;
  }

  /** Unseal a cookie reference back into its session id, or `null` on failure. */
  private async refToId(ref: string): Promise<string | null> {
    try {
      const result: unknown = await unseal(webCrypto, ref, this.password, defaults);
      return result as string;
    } catch {
      return null;
    }
  }

  async read(ref: string): Promise<BffSession | null> {
    const id: string | null = await this.refToId(ref);
    if (id === null) {
      return null;
    }
    const raw: string | null = await this.client.get(this.sessionKey(id));
    if (raw === null) {
      return null;
    }
    try {
      return JSON.parse(raw) as BffSession;
    } catch {
      return null;
    }
  }

  async write(session: BffSession): Promise<string> {
    const id: string = session.sessionId || randomUrlSafe(SESSION_ID_BYTES);
    const record: BffSession = { ...session, sessionId: id };
    await this.client.set(this.sessionKey(id), JSON.stringify(record), {
      ex: this.ttlSeconds,
    });
    return seal(webCrypto, id, this.password, defaults);
  }

  async destroy(ref: string): Promise<void> {
    const id: string | null = await this.refToId(ref);
    if (id === null) {
      return;
    }
    await this.client.del(this.sessionKey(id));
  }

  async withRefreshLock<T>(ref: string, fn: () => Promise<T>): Promise<T | undefined> {
    const id: string | null = await this.refToId(ref);
    if (id === null) {
      return undefined;
    }
    const key: string = this.lockKey(id);
    const acquired: "OK" | null = await this.client.set(key, "1", {
      nx: true,
      ex: this.lockTtlSeconds,
    });
    if (acquired === null) {
      return undefined;
    }
    try {
      return await fn();
    } finally {
      await this.client.del(key);
    }
  }
}
