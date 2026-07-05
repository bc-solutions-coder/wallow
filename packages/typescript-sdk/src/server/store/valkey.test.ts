import { describe, expect, it } from "vitest";

import { type BffSession } from "../session";
import { type RedisLike, type SessionStore } from "./types";
import { ValkeySessionStore } from "./valkey";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";
const SESSION_ID: string = "sess-fixture-000";
const SESSION_KEY: string = `wallow:session:${SESSION_ID}`;

function makeSession(): BffSession {
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
  };
}

/**
 * In-memory {@link RedisLike} fake backed by a Map, honoring the `nx` and `ex`
 * flags the store relies on. `now` is a settable virtual clock (milliseconds)
 * so expiry can be exercised deterministically.
 */
class FakeRedis implements RedisLike {
  public readonly store: Map<string, { value: string; expiresAt: number | null }> =
    new Map();
  public now: number = 0;

  get(key: string): Promise<string | null> {
    const entry: { value: string; expiresAt: number | null } | undefined =
      this.store.get(key);
    if (entry === undefined) {
      return Promise.resolve(null);
    }
    if (entry.expiresAt !== null && entry.expiresAt <= this.now) {
      this.store.delete(key);
      return Promise.resolve(null);
    }
    return Promise.resolve(entry.value);
  }

  set(
    key: string,
    value: string,
    opts?: { ex?: number; nx?: boolean },
  ): Promise<"OK" | null> {
    if (opts?.nx === true) {
      const existing: { value: string; expiresAt: number | null } | undefined =
        this.store.get(key);
      const alive: boolean =
        existing !== undefined &&
        (existing.expiresAt === null || existing.expiresAt > this.now);
      if (alive) {
        return Promise.resolve(null);
      }
    }
    const expiresAt: number | null =
      opts?.ex !== undefined ? this.now + opts.ex * 1000 : null;
    this.store.set(key, { value, expiresAt });
    return Promise.resolve("OK");
  }

  del(key: string): Promise<number> {
    return Promise.resolve(this.store.delete(key) ? 1 : 0);
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
    const first: Promise<string | undefined> = store.withRefreshLock(
      ref,
      async () => {
        signalStarted();
        await releaseGate;
        return "first-result";
      },
    );

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
    const third: string | undefined = await store.withRefreshLock(
      ref,
      async () => "third-result",
    );
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
