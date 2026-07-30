/**
 * Spec for the role/permission helpers (Wallow-x4qn.3).
 *
 * These read the TYPED arrays `UsersController.GetCurrentUser` answers with, so
 * the contract they owe is the SERVER's contract — a browser helper that answers
 * differently from the API is worse than no helper, because the UI then promises
 * something the next request refuses. The API is the reason for each casing rule
 * below:
 *
 *   - ROLES are case-INSENSITIVE. `ClaimsPrincipalExtensions.GetRoles()`
 *     deduplicates with `StringComparer.OrdinalIgnoreCase` and
 *     `AuthorizationController` builds its role set the same way. It is also
 *     forced by this package's own barrel: `isAdmin` is re-exported from the SDK
 *     and IS case-insensitive, so a case-sensitive `hasRole` here would let
 *     `hasRole(user, "admin")` and `isAdmin(user)` disagree about the same user
 *     from the same import.
 *   - PERMISSIONS are case-SENSITIVE. `PermissionAuthorizationHandler` decides
 *     with a plain `permissions.Contains(requirement.Permission)` — ordinal. A
 *     lenient browser check would show a control the API then refuses, which is
 *     the one direction that produces a broken screen rather than a hidden one.
 *
 * Surrounding whitespace is trimmed off the name being looked for in both cases:
 * no role or permission the API issues has any, so it is always caller noise.
 *
 * Anonymous and claimless users answer `false` rather than throwing. Every call
 * site is a UI gate, and a gate that throws takes the screen down instead of
 * hiding a button.
 */

import { isAdmin } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import { hasPermission, hasRole } from "./authorization";
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

  it("agrees with the isAdmin this package re-exports, for the same user", () => {
    // The concrete reason roles are case-insensitive: both symbols come out of
    // this package's barrel, so they may not disagree about one user.
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
