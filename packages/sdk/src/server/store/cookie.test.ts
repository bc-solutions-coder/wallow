import { afterEach, describe, expect, it, vi } from "vitest";

import { DEFAULT_SESSION_TTL_SECONDS } from "../config";
import { type BffSession } from "../session";
import { CookieSessionStore } from "./cookie";
import { type SessionStore } from "./types";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";

/** iron-webcrypto's default `timestampSkewSec` (60s), expressed in ms. */
const IRON_SKEW_MS: number = 60_000;

/** Arbitrary fixed wall clock the ttl specs seal against. */
const BASE_NOW_MS: number = 1_700_000_000_000;

/** Pin `Date.now()`, which is the clock iron seals and validates against. */
function freezeNow(atMs: number): void {
  vi.spyOn(Date, "now").mockReturnValue(atMs);
}

function makeSession(): BffSession {
  return {
    sessionId: "sess-fixture-000",
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

describe("CookieSessionStore", () => {
  it("write returns a ref that read round-trips back to the session", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const session: BffSession = makeSession();

    const ref: string = await store.write(session);
    const result: BffSession | null = await store.read(ref);

    expect(typeof ref).toBe("string");
    expect(result).toEqual(session);
  });

  it("read returns null for a garbage ref", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });

    const result: BffSession | null = await store.read("not-a-sealed-token");

    expect(result).toBeNull();
  });

  it("destroy is a no-op that resolves", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const session: BffSession = makeSession();
    const ref: string = await store.write(session);

    await expect(store.destroy(ref)).resolves.toBeUndefined();
  });

  it("withRefreshLock runs fn directly and returns its value", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });

    let ran: boolean = false;
    const result: string | undefined = await store.withRefreshLock("any-ref", async () => {
      ran = true;
      return "fn-result";
    });

    expect(ran).toBe(true);
    expect(result).toBe("fn-result");
  });
});

/** A promise plus the resolver that settles it, for gating a test's interleaving. */
interface Deferred<T> {
  readonly promise: Promise<T>;
  readonly resolve: (value: T) => void;
  readonly reject: (reason: Error) => void;
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason: Error) => void;
  const promise: Promise<T> = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

/**
 * Drain the microtask queue so an assertion about "has this callback started
 * yet?" observes settled state rather than a pending continuation. Deterministic
 * — no real timers, so a wrongly-serializing implementation fails the ordering
 * assertion instead of merely running slowly.
 */
async function flushMicrotasks(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

/**
 * The in-process refresh mutex (Wallow-vufu.5.3).
 *
 * `withRefreshLock` is a no-op passthrough today, so two requests arriving in
 * the same Node process for the same session (two browser tabs, one expired
 * access token) both run the refresh callback, and both spend the SAME one-time
 * OIDC refresh token — the second spend fails or invalidates the token family.
 *
 * Serializing the callbacks is not enough to fix that: the caller
 * (`refreshUnderLock` in proxy.ts) captures `session.refreshToken` BEFORE taking
 * the lock, so a second callback that runs afterwards still presents the token
 * the first one just spent. The lock must therefore COALESCE: while a refresh is
 * in flight for a ref, a second caller joins it and observes the first call's
 * result instead of running its own callback. That is the cookie store's
 * equivalent of the Valkey store's "lock held → re-read the peer's write"; the
 * cookie IS the state, so there is nothing to re-read and the in-flight promise
 * is the only handle on the peer's result.
 *
 * For this store the `ref` argument is the sealed cookie string itself, so two
 * concurrent requests carrying the same browser session present an identical ref
 * and coalesce, while distinct sessions carry distinct refs and never interact.
 */
describe("CookieSessionStore — in-process refresh mutex", () => {
  it("coalesces concurrent locks for the same ref: the callback runs once and both callers get its result", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const ref: string = "sealed-cookie-ref-for-one-browser-session";
    const release: Deferred<void> = deferred<void>();
    const started: Deferred<void> = deferred<void>();

    let firstRuns: number = 0;
    let secondRuns: number = 0;

    const first: Promise<string | undefined> = store.withRefreshLock(ref, async () => {
      firstRuns += 1;
      started.resolve();
      await release.promise;
      return "first-result";
    });

    // The first callback is running and has not settled.
    await started.promise;

    const second: Promise<string | undefined> = store.withRefreshLock(ref, async () => {
      secondRuns += 1;
      return "second-result";
    });

    // The second callback must not have been invoked — not now, and not once
    // the microtask queue drains. Running it later would still re-spend the
    // refresh token the first callback is spending right now.
    await flushMicrotasks();
    expect(secondRuns).toBe(0);

    release.resolve();

    expect(await first).toBe("first-result");
    expect(await second).toBe("first-result");
    expect(firstRuns).toBe(1);
    expect(secondRuns).toBe(0);
  });

  it("runs a fresh callback for a lock taken after the previous one settled", async () => {
    // The mutex must not pin the first result: a leaked map entry would make a
    // later request adopt a stale (already-expired) session forever.
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const ref: string = "sealed-cookie-ref-sequential";

    const firstResult: string | undefined = await store.withRefreshLock(ref, async () => "first");
    const secondResult: string | undefined = await store.withRefreshLock(ref, async () => "second");

    expect(firstResult).toBe("first");
    expect(secondResult).toBe("second");
  });

  it("propagates a rejected callback to every joined caller and leaves the ref usable", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const ref: string = "sealed-cookie-ref-rejecting";
    const release: Deferred<void> = deferred<void>();
    const started: Deferred<void> = deferred<void>();
    const failure: Error = new Error("refresh_token rejected by the issuer");

    let secondRuns: number = 0;

    const first: Promise<string | undefined> = store.withRefreshLock(ref, async () => {
      started.resolve();
      await release.promise;
      throw failure;
    });

    await started.promise;

    const second: Promise<string | undefined> = store.withRefreshLock(ref, async () => {
      secondRuns += 1;
      return "second-result";
    });

    release.resolve();

    await expect(first).rejects.toThrow(failure);
    // The joined caller sees the same failure rather than silently succeeding
    // with a refresh of its own against the already-spent token.
    await expect(second).rejects.toThrow(failure);
    expect(secondRuns).toBe(0);

    // A failed refresh must release the lock, not poison the ref.
    await expect(store.withRefreshLock(ref, async () => "recovered")).resolves.toBe("recovered");
  });

  it("does not serialize across different refs", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });
    const release: Deferred<void> = deferred<void>();
    const started: Deferred<void> = deferred<void>();
    const order: string[] = [];

    const first: Promise<string | undefined> = store.withRefreshLock("ref-session-a", async () => {
      order.push("a-start");
      started.resolve();
      await release.promise;
      order.push("a-end");
      return "a-result";
    });

    await started.promise;

    // A different session's refresh completes while the first is still held.
    const second: string | undefined = await store.withRefreshLock("ref-session-b", async () => {
      order.push("b-start");
      return "b-result";
    });

    expect(second).toBe("b-result");
    expect(order).toEqual(["a-start", "b-start"]);

    release.resolve();
    expect(await first).toBe("a-result");
    expect(order).toEqual(["a-start", "b-start", "a-end"]);
  });
});

describe("CookieSessionStore — session ttl", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("read returns null for a ref written longer ago than ttlSeconds", async () => {
    const ttlSeconds: number = 3600;
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD, ttlSeconds });

    freezeNow(BASE_NOW_MS);
    const ref: string = await store.write(makeSession());

    freezeNow(BASE_NOW_MS + ttlSeconds * 1000 + IRON_SKEW_MS + 1000);
    const result: BffSession | null = await store.read(ref);

    expect(result).toBeNull();
  });

  it("read still returns the session inside the ttl window", async () => {
    const ttlSeconds: number = 3600;
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD, ttlSeconds });
    const session: BffSession = makeSession();

    freezeNow(BASE_NOW_MS);
    const ref: string = await store.write(session);

    freezeNow(BASE_NOW_MS + (ttlSeconds * 1000) / 2);
    const result: BffSession | null = await store.read(ref);

    expect(result).toEqual(session);
  });

  it("falls back to the default session ttl when ttlSeconds is omitted", async () => {
    const store: SessionStore = new CookieSessionStore({ password: PASSWORD });

    freezeNow(BASE_NOW_MS);
    const ref: string = await store.write(makeSession());

    freezeNow(BASE_NOW_MS + DEFAULT_SESSION_TTL_SECONDS * 1000 + IRON_SKEW_MS + 1000);
    const result: BffSession | null = await store.read(ref);

    expect(result).toBeNull();
  });
});
