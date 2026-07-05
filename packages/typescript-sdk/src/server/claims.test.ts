import { describe, expect, it } from "vitest";

import { decodeIdTokenClaims } from "./claims";
import type { BffSession } from "./session";

/**
 * Build a minimal JWT-shaped string with the given payload. Signature
 * verification is not performed by the decoder, so a fixed dummy segment is
 * used for the header and signature.
 */
function makeJwt(payload: Record<string, unknown>): string {
  const header: string = Buffer.from(
    JSON.stringify({ alg: "RS256", typ: "JWT" }),
  ).toString("base64url");
  const body: string = Buffer.from(JSON.stringify(payload)).toString(
    "base64url",
  );
  return `${header}.${body}.signature`;
}

describe("decodeIdTokenClaims", () => {
  it("extracts sub, email, and name from the payload", () => {
    const token: string = makeJwt({
      sub: "user-123",
      email: "user@example.com",
      name: "Test User",
    });

    const user: BffSession["user"] = decodeIdTokenClaims(token);

    expect(user.sub).toBe("user-123");
    expect(user.email).toBe("user@example.com");
    expect(user.name).toBe("Test User");
  });

  it("returns a sub-only user when optional claims are absent", () => {
    const token: string = makeJwt({ sub: "user-456" });

    const user: BffSession["user"] = decodeIdTokenClaims(token);

    expect(user.sub).toBe("user-456");
    expect(user.email).toBeUndefined();
    expect(user.name).toBeUndefined();
  });

  it("throws on a malformed token", () => {
    expect(() => decodeIdTokenClaims("not-a-jwt")).toThrow();
  });
});
