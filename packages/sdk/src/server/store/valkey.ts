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

import { type CookieSecret } from "../config";
import { sealPassword, unsealPassword } from "../cookie-secret";
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
 * Root of every key namespace: the store's default `keyPrefix`, and the base
 * the server preset builds `wallow:<appId>` prefixes from.
 */
export const DEFAULT_KEY_PREFIX: string = "wallow";

/** A claim round found the owner marker expired mid-read; fight the round again. */
const CONTEND: unique symbol = Symbol("contend");

/**
 * Options for {@link ValkeySessionStore}.
 */
export interface ValkeySessionStoreOptions {
  /** The Redis-compatible client used to persist sessions and locks. */
  client: RedisLike;
  /**
   * The cookie password used to seal and unseal session references (>= 32
   * chars), or a keyed set during a rotation — every key in it can unseal a
   * reference, and its active key seals new ones.
   */
  password: CookieSecret;
  /** Session record time-to-live in seconds. Defaults to `86400` (one day). */
  ttlSeconds?: number;
  /** Refresh-lock time-to-live in seconds. Defaults to `10`. */
  lockTtlSeconds?: number;
  /** Namespace prefix for all keys. Defaults to `"wallow"`. */
  keyPrefix?: string;
}

/**
 * Persist BFF sessions in Redis or Valkey with sealed cookie references.
 *
 * Stores tokens server-side and maintains session-ID and subject indexes for back-channel
 * logout. Use a distinct keyPrefix for each BFF identity, or set BFF_APP_ID with
 * createWallowBffServer. Session writes reset the configured record TTL; refresh locks are
 * shared across instances using the same namespace.
 */
export class ValkeySessionStore implements SessionStore {
  private readonly client: RedisLike;
  private readonly password: CookieSecret;
  private readonly ttlSeconds: number;
  private readonly lockTtlSeconds: number;
  private readonly keyPrefix: string;

  /**
   * Create a session store over the supplied RedisLike client. Defaults to a one-day record
   * TTL, ten-second refresh locks, and the wallow key prefix. The host owns client
   * connection setup and teardown.
   */
  constructor(options: ValkeySessionStoreOptions) {
    this.client = options.client;
    this.password = options.password;
    this.ttlSeconds = options.ttlSeconds ?? DEFAULT_TTL_SECONDS;
    this.lockTtlSeconds = options.lockTtlSeconds ?? DEFAULT_LOCK_TTL_SECONDS;
    this.keyPrefix = options.keyPrefix ?? DEFAULT_KEY_PREFIX;
  }

  private sessionKey(id: string): string {
    return `${this.keyPrefix}:session:${id}`;
  }

  private lockKey(id: string): string {
    return `${this.keyPrefix}:refreshlock:${id}`;
  }

  private sidKey(sid: string): string {
    return `${this.keyPrefix}:sid:${sid}`;
  }

  private subjectKey(sub: string): string {
    return `${this.keyPrefix}:sub:${sub}`;
  }

  private ownerKey(): string {
    return `${this.keyPrefix}:owner`;
  }

  /**
   * Record the BFF identity occupying this key namespace. Returns null when the claim
   * succeeds or matches, otherwise the conflicting owner. Matching claims renew the marker
   * for the session TTL.
   */
  async claimNamespace(owner: string): Promise<string | null> {
    const key: string = this.ownerKey();
    const first: string | null | typeof CONTEND = await this.attemptClaim(key, owner);
    if (first !== CONTEND) {
      return first;
    }
    // The marker expired between the conditional set and the read. An
    // unconditional write here could let two concurrent claimants each
    // conclude the namespace is theirs, so contend through SET NX once more;
    // a second vanished marker is reported as ours — the next boot settles it.
    const second: string | null | typeof CONTEND = await this.attemptClaim(key, owner);
    return second === CONTEND ? null : second;
  }

  /**
   * One claim round: `null` means the namespace is ours, a string is the rival
   * identity holding it, {@link CONTEND} means the marker expired between the
   * conditional set and the read and the round must be re-fought.
   */
  private async attemptClaim(key: string, owner: string): Promise<string | null | typeof CONTEND> {
    const claimed: "OK" | null = await this.client.set(key, owner, {
      nx: true,
      ex: this.ttlSeconds,
    });
    if (claimed === "OK") {
      return null;
    }
    const standing: string | null = await this.client.get(key);
    if (standing === owner) {
      await this.client.set(key, owner, { ex: this.ttlSeconds });
      return null;
    }
    return standing ?? CONTEND;
  }

  /** Unseal a cookie reference back into its session id, or `null` on failure. */
  private async refToId(ref: string): Promise<string | null> {
    try {
      const result: unknown = await unseal(webCrypto, ref, unsealPassword(this.password), defaults);
      return result as string;
    } catch {
      return null;
    }
  }

  /**
   * Resolve a sealed reference to its server-side session record. Returns null for an
   * invalid reference, missing record, or unreadable JSON. Redis failures propagate.
   */
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

  /**
   * Persist the session and update its sid and subject indexes, resetting their TTLs. Reuses
   * sessionId when present or generates one, then returns a sealed reference for the browser
   * cookie. Redis and sealing failures propagate.
   */
  async write(session: BffSession): Promise<string> {
    const id: string = session.sessionId || randomUrlSafe(SESSION_ID_BYTES);
    const record: BffSession = { ...session, sessionId: id };
    await this.client.set(this.sessionKey(id), JSON.stringify(record), {
      ex: this.ttlSeconds,
    });
    if (record.sid !== undefined) {
      await this.client.set(this.sidKey(record.sid), id, { ex: this.ttlSeconds });
    }
    // The subject set is shared across the subject's sessions, so each write
    // pushes its expiry out to the newest record's lifetime.
    await this.client.sadd(this.subjectKey(record.user.sub), id);
    await this.client.expire(this.subjectKey(record.user.sub), this.ttlSeconds);
    return seal(webCrypto, id, sealPassword(this.password), defaults);
  }

  /**
   * Delete the session record and its index entries. Invalid references are ignored. Does
   * not clear browser cookies or revoke tokens at the issuer.
   */
  async destroy(ref: string): Promise<void> {
    const id: string | null = await this.refToId(ref);
    if (id === null) {
      return;
    }
    await this.destroyRecord(id);
  }

  /** Delete the record at `id` and its index entries, returning the session. */
  private async destroyRecord(id: string): Promise<BffSession | null> {
    const raw: string | null = await this.client.get(this.sessionKey(id));
    let record: BffSession | null = null;
    if (raw !== null) {
      try {
        record = JSON.parse(raw) as BffSession;
      } catch {
        record = null;
      }
    }
    await this.client.del(this.sessionKey(id));
    if (record !== null) {
      if (record.sid !== undefined) {
        await this.client.del(this.sidKey(record.sid));
      }
      await this.client.srem(this.subjectKey(record.user.sub), id);
    }
    return record;
  }

  /**
   * Delete the session currently indexed by the issuer sid and return its record. If
   * multiple logins used the same sid, only the latest indexed session is targeted. Missing
   * records return an empty array.
   */
  async revokeBySid(sid: string): Promise<BffSession[]> {
    const id: string | null = await this.client.get(this.sidKey(sid));
    if (id === null) {
      return [];
    }
    const record: BffSession | null = await this.destroyRecord(id);
    // The index itself may outlive an evicted record; clear it either way.
    await this.client.del(this.sidKey(sid));
    return record === null ? [] : [record];
  }

  /**
   * Delete every session indexed for the issuer subject and return the removed records.
   * Prunes stale index members. Does not itself revoke tokens at the issuer.
   */
  async revokeBySubject(sub: string): Promise<BffSession[]> {
    const ids: string[] = await this.client.smembers(this.subjectKey(sub));
    const records: (BffSession | null)[] = await Promise.all(
      ids.map(async (id: string): Promise<BffSession | null> => {
        const record: BffSession | null = await this.destroyRecord(id);
        if (record === null) {
          // Stale member whose record was evicted out of band: prune it.
          await this.client.srem(this.subjectKey(sub), id);
        }
        return record;
      }),
    );
    return records.filter((record: BffSession | null): record is BffSession => record !== null);
  }

  /**
   * Attempt the shared refresh lock and run fn only when acquired. Returns undefined
   * immediately for an invalid reference or a contended lock. Releases the lock after fn
   * settles; lockTtlSeconds bounds the lock lifetime.
   */
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
