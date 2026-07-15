import { describe, expect, it } from "vitest";

import { decodeIdTokenClaims, mapClaims } from "./claims";
import type { BffSession } from "./session";

/**
 * Build a minimal JWT-shaped string with the given payload. Signature
 * verification is not performed by the decoder, so a fixed dummy segment is
 * used for the header and signature.
 */
function makeJwt(payload: Record<string, unknown>): string {
  const header: string = Buffer.from(JSON.stringify({ alg: "RS256", typ: "JWT" })).toString(
    "base64url",
  );
  const body: string = Buffer.from(JSON.stringify(payload)).toString("base64url");
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

describe("mapClaims", () => {
  it("normalizes a single string `role` claim into a roles array", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      role: "admin",
    });

    expect(user.roles).toEqual(["admin"]);
  });

  it("passes a `roles` array claim through into roles", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      roles: ["user", "editor"],
    });

    expect(user.roles).toEqual(["user", "editor"]);
  });

  it("merges a string `role` and an array `roles` claim into roles", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      role: "admin",
      roles: ["user", "editor"],
    });

    // Both the singular and plural role claims are surfaced in the array.
    expect(user.roles).toEqual(expect.arrayContaining(["admin", "user", "editor"]));
    expect(user.roles).toHaveLength(3);
  });

  it("normalizes a `permissions` array claim into permissions", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      permissions: ["read", "write"],
    });

    expect(user.permissions).toEqual(["read", "write"]);
  });

  it("splits a space-delimited `scope` string into permissions", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      scope: "read write delete",
    });

    expect(user.permissions).toEqual(expect.arrayContaining(["read", "write", "delete"]));
    expect(user.permissions).toHaveLength(3);
  });

  it("lifts tenant_id and tenant_name into first-class fields", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-1",
      tenant_id: "tenant-42",
      tenant_name: "Acme Corp",
    });

    expect(user.tenantId).toBe("tenant-42");
    expect(user.tenantName).toBe("Acme Corp");
  });

  it("preserves sub and passes unknown claims through the index signature", () => {
    const user: BffSession["user"] = mapClaims({
      sub: "user-99",
      email: "user@example.com",
      custom_claim: "custom-value",
    });

    expect(user.sub).toBe("user-99");
    expect(user.email).toBe("user@example.com");
    expect(user.custom_claim).toBe("custom-value");
  });
});
