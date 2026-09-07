/**
 * Verify role and permission casing, anonymous results, and the separation between organization
 * and global administrators.
 */

import { describe, expect, it } from "vitest";

import { hasPermission, hasRole, isAdmin, isGlobalAdmin } from "./authorization";
import type { CurrentUser } from "./current-user";

const ADMIN: CurrentUser = {
  sub: "3f1c4b0e-0000-4000-8000-000000000001",
  id: "3f1c4b0e-0000-4000-8000-000000000001",
  email: "admin@wallow.dev",
  roles: ["Admin", "Member"],
  permissions: ["users.read", "users.write"],
};

/** A signed-in user the API answered for before any role or permission was granted. */
const CLAIMLESS: CurrentUser = {
  sub: "3f1c4b0e-0000-4000-8000-000000000002",
  id: "3f1c4b0e-0000-4000-8000-000000000002",
  email: "new@wallow.dev",
};

const EMPTY_ARRAYS: CurrentUser = { ...CLAIMLESS, roles: [], permissions: [] };

describe("hasRole", () => {
  it("finds a role the user holds", () => {
    expect(hasRole(ADMIN, "Admin")).toBe(true);
    expect(hasRole(ADMIN, "Member")).toBe(true);
  });

  it("compares case-insensitively, matching the API's OrdinalIgnoreCase role sets", () => {
    expect(hasRole(ADMIN, "admin")).toBe(true);
    expect(hasRole(ADMIN, "ADMIN")).toBe(true);
  });

  it("agrees with isAdmin, for the same user", () => {
    // Both symbols come out of this package's barrel, so they may not disagree
    // about one user.
    expect(hasRole(ADMIN, "admin")).toBe(isAdmin(ADMIN));
    expect(hasRole(CLAIMLESS, "admin")).toBe(isAdmin(CLAIMLESS));
  });

  it("trims the role being looked for", () => {
    expect(hasRole(ADMIN, "  Admin  ")).toBe(true);
  });

  it("is false for a role the user does not hold", () => {
    expect(hasRole(ADMIN, "Operator")).toBe(false);
  });

  it("is false for an anonymous user rather than throwing", () => {
    expect(hasRole(null, "Admin")).toBe(false);
    expect(hasRole(undefined, "Admin")).toBe(false);
  });

  it("is false when the API answered no roles at all", () => {
    expect(hasRole(CLAIMLESS, "Admin")).toBe(false);
    expect(hasRole(EMPTY_ARRAYS, "Admin")).toBe(false);
  });

  it("is false for a blank role name", () => {
    expect(hasRole(ADMIN, "")).toBe(false);
    expect(hasRole(ADMIN, "   ")).toBe(false);
  });
});

describe("isAdmin", () => {
  it("is true for a user holding the admin role, whatever its casing", () => {
    // The fixture holds "Admin" — the case-insensitive comparison is inherited
    // from hasRole.
    expect(isAdmin(ADMIN)).toBe(true);
  });

  it("is false for a signed-in non-admin", () => {
    expect(isAdmin({ ...CLAIMLESS, roles: ["Member"] })).toBe(false);
  });

  it("is false for an anonymous or claimless user rather than throwing", () => {
    expect(isAdmin(null)).toBe(false);
    expect(isAdmin(undefined)).toBe(false);
    expect(isAdmin(CLAIMLESS)).toBe(false);
  });
});

describe("hasPermission", () => {
  it("finds a permission the user holds", () => {
    expect(hasPermission(ADMIN, "users.read")).toBe(true);
    expect(hasPermission(ADMIN, "users.write")).toBe(true);
  });

  it("compares case-sensitively, matching the API's ordinal permission check", () => {
    // PermissionAuthorizationHandler would refuse "USERS.READ", so answering
    // true here would render a control the next request rejects.
    expect(hasPermission(ADMIN, "USERS.READ")).toBe(false);
    expect(hasPermission(ADMIN, "Users.Read")).toBe(false);
  });

  it("trims the permission being looked for", () => {
    expect(hasPermission(ADMIN, "  users.read  ")).toBe(true);
  });

  it("is false for a permission the user does not hold", () => {
    expect(hasPermission(ADMIN, "users.delete")).toBe(false);
  });

  it("is false for an anonymous user rather than throwing", () => {
    expect(hasPermission(null, "users.read")).toBe(false);
    expect(hasPermission(undefined, "users.read")).toBe(false);
  });

  it("is false when the API answered no permissions at all", () => {
    expect(hasPermission(CLAIMLESS, "users.read")).toBe(false);
    expect(hasPermission(EMPTY_ARRAYS, "users.read")).toBe(false);
  });

  it("is false for a blank permission name", () => {
    expect(hasPermission(ADMIN, "")).toBe(false);
    expect(hasPermission(ADMIN, "   ")).toBe(false);
  });
});

describe("isGlobalAdmin", () => {
  it("is true only when the API marked the user with the platform's own authority", () => {
    expect(isGlobalAdmin({ ...ADMIN, isGlobalAdmin: true })).toBe(true);
  });

  it("is false for an organization admin without the claim", () => {
    // The authority is minted at sign-in, never derived from roles — holding
    // "Admin" must not imply it.
    expect(isGlobalAdmin(ADMIN)).toBe(false);
    expect(isGlobalAdmin({ ...ADMIN, isGlobalAdmin: false })).toBe(false);
  });

  it("is false for an anonymous or claimless user rather than throwing", () => {
    expect(isGlobalAdmin(null)).toBe(false);
    expect(isGlobalAdmin(undefined)).toBe(false);
    expect(isGlobalAdmin(CLAIMLESS)).toBe(false);
  });
});
