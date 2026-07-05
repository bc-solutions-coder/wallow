import { describe, expect, it } from "vitest";

import {
  sealSession,
  unsealSession,
  type BffSession,
} from "./session";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";

function makeSession(): BffSession {
  return {
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
    const result: BffSession | null = await unsealSession(
      "not-a-sealed-token",
      PASSWORD,
    );

    expect(result).toBeNull();
  });
});
