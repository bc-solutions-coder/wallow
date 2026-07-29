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
