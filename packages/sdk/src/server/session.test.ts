import { afterEach, describe, expect, it, vi } from "vitest";

import {
  DEFAULT_COOKIE_KEY_ID,
  DEFAULT_SESSION_TTL_SECONDS,
  type CookiePasswordSet,
} from "./config";
import { sealSession, unsealSession, type BffSession } from "./session";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";

/**
 * iron-webcrypto's default `timestampSkewSec` (60s) is subtracted from "now"
 * before the expiry comparison, so a blob only reads as expired once it is that
 * much past its baked-in expiration.
 */
const IRON_SKEW_MS: number = 60_000;

/** Arbitrary fixed wall clock the ttl specs seal against. */
const BASE_NOW_MS: number = 1_700_000_000_000;

const DEFAULT_TTL_MS: number = DEFAULT_SESSION_TTL_SECONDS * 1000;

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
      org_id: "org-999",
    },
    version: 1,
  };
}

describe("sealSession / unsealSession", () => {
  it("round-trips to an equal session object", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD);

    expect(result).toEqual(session);
  });

  it("produces a sealed string that does not contain the raw access token", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);

    expect(typeof sealed).toBe("string");
    expect(sealed).not.toContain(session.accessToken);
    expect(sealed).not.toContain(session.refreshToken);
  });

  it("returns null when unsealed with the wrong password", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);
    const result: BffSession | null = await unsealSession(
      sealed,
      "a-different-cookie-password-of-at-least-32-chars",
    );

    expect(result).toBeNull();
  });

  it("returns null when the sealed token has been tampered with", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);
    const tampered: string = `${sealed.slice(0, -3)}xyz`;
    const result: BffSession | null = await unsealSession(tampered, PASSWORD);

    expect(result).toBeNull();
  });

  it("returns null for a completely invalid sealed string", async () => {
    const result: BffSession | null = await unsealSession("not-a-sealed-token", PASSWORD);

    expect(result).toBeNull();
  });

  it("round-trips the sessionId, version, and csrfToken fields", async () => {
    const session: BffSession = {
      sessionId: "sess-abc-123",
      accessToken: "access-token-abc-123",
      refreshToken: "refresh-token-def-456",
      idToken: "id-token-ghi-789",
      expiresAt: 1_700_000_000_000,
      user: {
        sub: "user-123",
        email: "user@example.com",
        name: "Test User",
        roles: ["admin"],
        permissions: ["read"],
        tenantId: "tenant-42",
        tenantName: "Acme",
      },
      version: 1,
      csrfToken: "csrf-token-xyz-789",
    };

    const sealed: string = await sealSession(session, PASSWORD);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD);

    expect(result).toEqual(session);
    expect(result?.sessionId).toBe("sess-abc-123");
    expect(result?.version).toBe(1);
    expect(result?.csrfToken).toBe("csrf-token-xyz-789");
  });
});

/**
 * Cookie-password rotation (finding L3).
 *
 * A rotation runs in three states, and a session sealed before it started has to
 * survive all of them: the old key alone, both keys with the NEW one sealing,
 * and finally the new key alone once the old cookies have expired. Only the last
 * state may invalidate anything.
 */
describe("sealSession / unsealSession — keyed password rotation", () => {
  const V1: string = "v1-cookie-password-of-at-least-32-chars";
  const V2: string = "v2-cookie-password-of-at-least-32-chars";

  /** The state before a rotation starts: v1 is the only key. */
  const ONLY_V1: CookiePasswordSet = { activeKeyId: "v1", keys: { v1: V1 } };
  /** Mid-rotation: v2 seals new cookies, v1 still unseals the old ones. */
  const V2_THEN_V1: CookiePasswordSet = { activeKeyId: "v2", keys: { v2: V2, v1: V1 } };
  /** After the old cookies aged out and v1 was dropped. */
  const ONLY_V2: CookiePasswordSet = { activeKeyId: "v2", keys: { v2: V2 } };

  it("unseals a cookie sealed under v1 with a set that still carries v1", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, ONLY_V1);
    const result: BffSession | null = await unsealSession(sealed, V2_THEN_V1);

    expect(result).toEqual(session);
  });

  it("seals new cookies under the ACTIVE key, not any other key in the set", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, V2_THEN_V1);

    // Behavioural proof: only a set holding v2 can read it back.
    expect(await unsealSession(sealed, ONLY_V2)).toEqual(session);
    expect(await unsealSession(sealed, ONLY_V1)).toBeNull();
  });

  it("stamps the active key ID into the sealed blob so iron can select it", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, V2_THEN_V1);

    // iron's wire format is `Fe26.2*<passwordId>*...`; the ID is what lets a
    // future unseal pick the right key without trying each one.
    expect(sealed.split("*")[1]).toBe("v2");
  });

  it("returns null once the sealing key has been dropped from the set", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, ONLY_V1);
    const result: BffSession | null = await unsealSession(sealed, ONLY_V2);

    // The end state of a rotation: old cookies stop working, by design, and
    // report it as a null session rather than throwing.
    expect(result).toBeNull();
  });

  it("returns null when a key ID matches but its secret does not", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, ONLY_V1);
    const impostor: CookiePasswordSet = { activeKeyId: "v1", keys: { v1: V2 } };

    expect(await unsealSession(sealed, impostor)).toBeNull();
  });

  /**
   * The upgrade path, and the reason the wrapping key ID must be `"default"`:
   * every cookie in every live browser was sealed by the bare-string API, and
   * the build that introduces the key map has to keep reading them.
   */
  it("unseals a cookie sealed by the OLD bare-string API using the wrapped set", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);
    const wrapped: CookiePasswordSet = {
      activeKeyId: DEFAULT_COOKIE_KEY_ID,
      keys: { [DEFAULT_COOKIE_KEY_ID]: PASSWORD },
    };

    expect(await unsealSession(sealed, wrapped)).toEqual(session);
  });

  /** The rollback path: a redeploy of the previous build must still read them. */
  it("unseals a cookie sealed under the wrapped set with the bare string again", async () => {
    const session: BffSession = makeSession();
    const wrapped: CookiePasswordSet = {
      activeKeyId: DEFAULT_COOKIE_KEY_ID,
      keys: { [DEFAULT_COOKIE_KEY_ID]: PASSWORD },
    };

    const sealed: string = await sealSession(session, wrapped);

    expect(await unsealSession(sealed, PASSWORD)).toEqual(session);
  });

  it("still round-trips a plain string password, unchanged", async () => {
    const session: BffSession = makeSession();

    const sealed: string = await sealSession(session, PASSWORD);

    expect(await unsealSession(sealed, PASSWORD)).toEqual(session);
  });

  it("honours the ttl argument with a keyed set exactly as with a string", async () => {
    const session: BffSession = makeSession();
    const ttlMs: number = 3_600_000;

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, V2_THEN_V1, ttlMs);

    freezeNow(BASE_NOW_MS + ttlMs / 2);
    expect(await unsealSession(sealed, V2_THEN_V1, ttlMs)).toEqual(session);

    freezeNow(BASE_NOW_MS + ttlMs + IRON_SKEW_MS + 1000);
    expect(await unsealSession(sealed, V2_THEN_V1, ttlMs)).toBeNull();

    vi.restoreAllMocks();
  });
});

describe("sealSession / unsealSession — expiry", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("unseals a blob that is still inside its explicit ttl", async () => {
    const session: BffSession = makeSession();
    const ttlMs: number = 3_600_000;

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, PASSWORD, ttlMs);

    freezeNow(BASE_NOW_MS + ttlMs / 2);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD, ttlMs);

    expect(result).toEqual(session);
  });

  it("returns null for a blob older than its explicit ttl", async () => {
    const session: BffSession = makeSession();
    const ttlMs: number = 3_600_000;

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, PASSWORD, ttlMs);

    freezeNow(BASE_NOW_MS + ttlMs + IRON_SKEW_MS + 1000);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD, ttlMs);

    expect(result).toBeNull();
  });

  it("cannot have an expired blob revived by unsealing it with a longer ttl", async () => {
    const session: BffSession = makeSession();
    const sealTtlMs: number = 60_000;

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, PASSWORD, sealTtlMs);

    freezeNow(BASE_NOW_MS + sealTtlMs + IRON_SKEW_MS + 1000);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD, DEFAULT_TTL_MS);

    expect(result).toBeNull();
  });

  it("bounds a blob sealed without an explicit ttl by the default session ttl", async () => {
    const session: BffSession = makeSession();

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, PASSWORD);

    freezeNow(BASE_NOW_MS + DEFAULT_TTL_MS + IRON_SKEW_MS + 1000);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD);

    expect(result).toBeNull();
  });

  it("keeps a blob sealed without an explicit ttl valid inside the default window", async () => {
    const session: BffSession = makeSession();

    freezeNow(BASE_NOW_MS);
    const sealed: string = await sealSession(session, PASSWORD);

    freezeNow(BASE_NOW_MS + DEFAULT_TTL_MS / 2);
    const result: BffSession | null = await unsealSession(sealed, PASSWORD);

    expect(result).toEqual(session);
  });
});
