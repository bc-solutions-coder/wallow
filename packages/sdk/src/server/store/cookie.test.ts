import { describe, expect, it } from "vitest";

import { type BffSession } from "../session";
import { CookieSessionStore } from "./cookie";
import { type SessionStore } from "./types";

const PASSWORD: string = "a-very-long-cookie-password-of-at-least-32-chars";

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
