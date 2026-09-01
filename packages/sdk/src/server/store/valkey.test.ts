import { describe, expect, it, vi } from "vitest";

import { type BffSession } from "../session";
import { type RedisLike, type SessionStore } from "./types";
import { ValkeySessionStore } from "./valkey";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";
const SESSION_ID: string = "sess-fixture-000";
const SESSION_KEY: string = `wallow:session:${SESSION_ID}`;

function makeSession(overrides: Partial<BffSession> = {}): BffSession {
  return {
    sessionId: SESSION_ID,
    accessToken: "access-token-abc-123",
    refreshToken: "refresh-token-def-456",
    idToken: "id-token-ghi-789",
    expiresAt: 1_700_000_000_000,
    user: {
      sub: "user-123",
      email: "user@example.com",
      name: "Test User",
    },
    version: 1,
    ...overrides,
  };
}

/**
 * In-memory {@link RedisLike} fake backed by a Map, honoring the `nx` and `ex`
 * flags the store relies on. `now` is a settable virtual clock (milliseconds)
 * so expiry can be exercised deterministically.
 */
class FakeRedis implements RedisLike {
  public readonly store: Map<string, { value: string; expiresAt: number | null }> = new Map();
  public now: number = 0;

  get(key: string): Promise<string | null> {
    const entry: { value: string; expiresAt: number | null } | undefined = this.store.get(key);
    if (entry === undefined) {
      return Promise.resolve(null);
    }
    if (entry.expiresAt !== null && entry.expiresAt <= this.now) {
      this.store.delete(key);
      return Promise.resolve(null);
    }
    return Promise.resolve(entry.value);
  }

  set(key: string, value: string, opts?: { ex?: number; nx?: boolean }): Promise<"OK" | null> {
    if (opts?.nx === true) {
      const existing: { value: string; expiresAt: number | null } | undefined = this.store.get(key);
      const alive: boolean =
        existing !== undefined && (existing.expiresAt === null || existing.expiresAt > this.now);
      if (alive) {
        return Promise.resolve(null);
      }
    }
    const expiresAt: number | null = opts?.ex !== undefined ? this.now + opts.ex * 1000 : null;
    this.store.set(key, { value, expiresAt });
    return Promise.resolve("OK");
  }

  del(key: string): Promise<number> {
    const hadString: boolean = this.store.delete(key);
    const hadSet: boolean = this.sets.delete(key);
    return Promise.resolve(hadString || hadSet ? 1 : 0);
  }

  /** Set entries, keyed like the string entries but holding members. */
  public readonly sets: Map<string, { members: Set<string>; expiresAt: number | null }> = new Map();

  private aliveSet(key: string): { members: Set<string>; expiresAt: number | null } | undefined {
    const entry: { members: Set<string>; expiresAt: number | null } | undefined =
      this.sets.get(key);
    if (entry === undefined) {
      return undefined;
    }
    if (entry.expiresAt !== null && entry.expiresAt <= this.now) {
      this.sets.delete(key);
      return undefined;
    }
    return entry;
  }

  sadd(key: string, member: string): Promise<number> {
    const entry: { members: Set<string>; expiresAt: number | null } = this.aliveSet(key) ?? {
      members: new Set<string>(),
      expiresAt: null,
    };
    const added: boolean = !entry.members.has(member);
    entry.members.add(member);
    this.sets.set(key, entry);
    return Promise.resolve(added ? 1 : 0);
  }

  srem(key: string, member: string): Promise<number> {
    const entry: { members: Set<string>; expiresAt: number | null } | undefined =
      this.aliveSet(key);
    if (entry === undefined) {
      return Promise.resolve(0);
    }
    const removed: boolean = entry.members.delete(member);
    if (entry.members.size === 0) {
      this.sets.delete(key);
    }
    return Promise.resolve(removed ? 1 : 0);
  }

  smembers(key: string): Promise<string[]> {
    return Promise.resolve([...(this.aliveSet(key)?.members ?? [])]);
  }

  expire(key: string, seconds: number): Promise<void> {
    const stringEntry: { value: string; expiresAt: number | null } | undefined =
      this.store.get(key);
    if (stringEntry !== undefined) {
      stringEntry.expiresAt = this.now + seconds * 1000;
    }
    const setEntry: { members: Set<string>; expiresAt: number | null } | undefined =
      this.sets.get(key);
    if (setEntry !== undefined) {
      setEntry.expiresAt = this.now + seconds * 1000;
    }
    return Promise.resolve();
  }

  /** Test helper: raw stored value at `key`, ignoring expiry. */
  raw(key: string): string | undefined {
    return this.store.get(key)?.value;
  }
}

describe("ValkeySessionStore", () => {
  it("write stores JSON at a namespaced key and returns a sealed ref that read round-trips", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: SessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const session: BffSession = makeSession();

    const ref: string = await store.write(session);

    // The server record is stored as JSON under the namespaced session key.
    const stored: string | undefined = redis.raw(SESSION_KEY);
    expect(stored).toBeDefined();
    expect(JSON.parse(stored as string)).toEqual(session);

    // The ref is a sealed handle: not the raw id and it leaks no user data.
    expect(typeof ref).toBe("string");
    expect(ref).not.toBe(SESSION_ID);
    expect(ref).not.toContain(SESSION_ID);
    expect(ref).not.toContain("access-token-abc-123");
    expect(ref).not.toContain("user@example.com");

    // read unseals the ref and returns the original session.
    const result: BffSession | null = await store.read(ref);
    expect(result).toEqual(session);
  });

  it("destroy deletes the server record so a subsequent read returns null (revocation)", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: SessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const ref: string = await store.write(makeSession());

    // Sanity: readable before revocation.
    expect(await store.read(ref)).not.toBeNull();

    await store.destroy(ref);

    expect(redis.raw(SESSION_KEY)).toBeUndefined();
    expect(await store.read(ref)).toBeNull();
  });

  it("withRefreshLock serializes concurrent refreshes: second acquire returns undefined while held, releases after fn", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: SessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const ref: string = await store.write(makeSession());

    let release!: () => void;
    let signalStarted!: () => void;
    const releaseGate: Promise<void> = new Promise<void>((resolve) => {
      release = resolve;
    });
    const startedGate: Promise<void> = new Promise<void>((resolve) => {
      signalStarted = resolve;
    });

    // First acquire holds the lock until we release it.
    const first: Promise<string | undefined> = store.withRefreshLock(ref, async () => {
      signalStarted();
      await releaseGate;
      return "first-result";
    });

    // Wait until the lock is held and the callback is running.
    await startedGate;

    // Second concurrent acquire cannot get the lock and returns undefined.
    const second: string | undefined = await store.withRefreshLock(
      ref,
      async () => "second-result",
    );
    expect(second).toBeUndefined();

    // Release the first; it completes with its value.
    release();
    expect(await first).toBe("first-result");

    // Lock is released after fn: a fresh acquire now succeeds.
    const third: string | undefined = await store.withRefreshLock(ref, async () => "third-result");
    expect(third).toBe("third-result");
  });

  it("read returns null for a missing server record", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: SessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const ref: string = await store.write(makeSession());

    // Simulate the record being evicted out of band.
    await redis.del(SESSION_KEY);

    expect(await store.read(ref)).toBeNull();
  });

  it("read returns null for an expired server record", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: SessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
      ttlSeconds: 60,
    });
    const ref: string = await store.write(makeSession());

    // Advance the virtual clock past the TTL.
    redis.now = 60 * 1000 + 1;

    expect(await store.read(ref)).toBeNull();
  });
});

describe("ValkeySessionStore revocation", () => {
  const SID: string = "op-session-abc";
  const SID_KEY: string = `wallow:sid:${SID}`;
  const SUB_KEY: string = "wallow:sub:user-123";

  it("write indexes sid and subject; revokeBySid destroys the session and both indexes and returns it", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const session: BffSession = makeSession({ sid: SID });
    await store.write(session);

    // Both indexes exist after write.
    expect(redis.raw(SID_KEY)).toBe(SESSION_ID);
    expect(await redis.smembers(SUB_KEY)).toEqual([SESSION_ID]);

    const revoked: BffSession[] = await store.revokeBySid(SID);

    expect(revoked).toEqual([session]);
    expect(redis.raw(SESSION_KEY)).toBeUndefined();
    expect(redis.raw(SID_KEY)).toBeUndefined();
    expect(await redis.smembers(SUB_KEY)).toEqual([]);
  });

  it("write without a sid indexes only the subject", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    await store.write(makeSession());

    expect(redis.raw(SID_KEY)).toBeUndefined();
    expect(await redis.smembers(SUB_KEY)).toEqual([SESSION_ID]);
  });

  it("revokeBySid of an unknown sid returns an empty list", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });

    expect(await store.revokeBySid("never-seen")).toEqual([]);
  });

  it("revokeBySubject destroys every session of the subject and returns them", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const first: BffSession = makeSession({ sid: SID });
    const second: BffSession = makeSession({
      sessionId: "sess-fixture-001",
      sid: "op-session-def",
    });
    await store.write(first);
    await store.write(second);

    const revoked: BffSession[] = await store.revokeBySubject("user-123");

    expect(revoked).toHaveLength(2);
    expect(revoked).toEqual(expect.arrayContaining([first, second]));
    expect(redis.raw(SESSION_KEY)).toBeUndefined();
    expect(redis.raw("wallow:session:sess-fixture-001")).toBeUndefined();
    expect(redis.raw(SID_KEY)).toBeUndefined();
    expect(redis.raw("wallow:sid:op-session-def")).toBeUndefined();
    expect(await redis.smembers(SUB_KEY)).toEqual([]);
  });

  it("revokeBySubject skips index members whose records are already gone", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    await store.write(makeSession());

    // Simulate the record being evicted out of band, leaving a stale index member.
    await redis.del(SESSION_KEY);

    expect(await store.revokeBySubject("user-123")).toEqual([]);
  });

  it("revokeBySubject of an unknown subject returns an empty list", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });

    expect(await store.revokeBySubject("never-seen")).toEqual([]);
  });

  it("destroy clears the sid and subject index entries alongside the record", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    const ref: string = await store.write(makeSession({ sid: SID }));

    await store.destroy(ref);

    expect(redis.raw(SESSION_KEY)).toBeUndefined();
    expect(redis.raw(SID_KEY)).toBeUndefined();
    expect(await redis.smembers(SUB_KEY)).toEqual([]);
  });
});

/**
 * The namespace claim behind the multi-BFF misconfiguration warning (#159):
 * two BFFs sharing one Valkey and one key prefix silently read each other's
 * sessions, so the first booter stamps the prefix with its identity and later
 * claimers with a DIFFERENT identity get that identity back to warn about.
 */
describe("ValkeySessionStore.claimNamespace", () => {
  const OWNER: string = "https://auth.example.com wallow-web";
  const OTHER_OWNER: string = "https://auth.example.com bff-example";

  it("first claim stamps the prefix and reports no conflict", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });

    expect(await store.claimNamespace(OWNER)).toBeNull();
    expect(redis.raw("wallow:owner")).toBe(OWNER);
  });

  it("re-claiming with the same identity stays quiet and refreshes the marker's TTL", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
      ttlSeconds: 60,
    });
    await store.claimNamespace(OWNER);
    redis.now = 50_000;

    expect(await store.claimNamespace(OWNER)).toBeNull();

    // A marker refreshed at t=50s under a 60s TTL is still alive at t=100s.
    redis.now = 100_000;
    expect(await redis.get("wallow:owner")).toBe(OWNER);
  });

  it("a different identity gets the standing owner back and does not overwrite it", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
    });
    await store.claimNamespace(OWNER);

    expect(await store.claimNamespace(OTHER_OWNER)).toBe(OWNER);
    expect(redis.raw("wallow:owner")).toBe(OWNER);
  });

  it("claims under the configured key prefix, so distinct prefixes never conflict", async () => {
    const redis: FakeRedis = new FakeRedis();
    const web: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
      keyPrefix: "wallow:web",
    });
    const example: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
      keyPrefix: "wallow:example",
    });

    expect(await web.claimNamespace(OWNER)).toBeNull();
    expect(await example.claimNamespace(OTHER_OWNER)).toBeNull();
    expect(redis.raw("wallow:web:owner")).toBe(OWNER);
    expect(redis.raw("wallow:example:owner")).toBe(OTHER_OWNER);
  });

  it("an expired marker can be claimed by a new identity", async () => {
    const redis: FakeRedis = new FakeRedis();
    const store: ValkeySessionStore = new ValkeySessionStore({
      client: redis,
      password: PASSWORD,
      ttlSeconds: 60,
    });
    await store.claimNamespace(OWNER);
    redis.now = 61_000;

    expect(await store.claimNamespace(OTHER_OWNER)).toBeNull();
    expect(redis.raw("wallow:owner")).toBe(OTHER_OWNER);
  });

  it("losing the expiry race to a rival claimant reports the rival, not ownership", async () => {
    // Script the exact interleaving: our SET NX loses, the marker has expired
    // by the time we GET (null), and the rival wins the re-contended SET NX.
    // An unconditional write here would let BOTH claimants conclude the
    // namespace is theirs and neither would warn.
    const replies: Array<"OK" | null> = [null, null];
    const client: RedisLike = {
      get: vi
        .fn<(key: string) => Promise<string | null>>()
        .mockResolvedValueOnce(null)
        .mockResolvedValue(OTHER_OWNER),
      set: (): Promise<"OK" | null> => Promise.resolve(replies.shift() ?? null),
      del: () => Promise.resolve(0),
      sadd: () => Promise.resolve(0),
      srem: () => Promise.resolve(0),
      smembers: () => Promise.resolve([]),
      expire: () => Promise.resolve(),
    };
    const store: ValkeySessionStore = new ValkeySessionStore({
      client,
      password: PASSWORD,
    });

    expect(await store.claimNamespace(OWNER)).toBe(OTHER_OWNER);
  });
});
