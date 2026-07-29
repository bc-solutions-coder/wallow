import { afterEach, describe, expect, it, vi } from "vitest";

import { DEFAULT_SESSION_TTL_SECONDS } from "./config";
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
