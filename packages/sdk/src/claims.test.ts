import { describe, expect, it } from "vitest";

import type { WallowUser } from "./auth";
import {
  getOrgId,
  getOrgName,
  getRoles,
  hasRole,
  isAdmin,
  isGlobalAdmin,
  isOperator,
} from "./claims";
import * as browserEntry from "./index";

/**
 * Spec (Wallow-pu6a.5.6): browser-side claim helpers.
 *
 * These replace the per-app hand-rolled claim reading — wallow-web's
 * `/dashboard` route carried its own `isAdminUser()` coping with `roles` vs
 * `role` and array-vs-string inline — and they must agree with what the API
 * enforces (`Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions`):
 *
 *   (a) roles read from `roles` with a `role` fallback, compared
 *       case-insensitively, deduplicated OrdinalIgnoreCase (first spelling
 *       wins) exactly like `GetRoles()`;
 *   (b) `is_operator`/`is_global_admin` are BOOLEAN claims parsed the way
 *       `bool.TryParse` parses them — a literal true only;
 *   (c) global admin is NOT a role and never leaks into `isAdmin()`; the API
 *       keeps the two checks separate on purpose, and conflating them here
 *       would re-create the role/claim confusion that was removed server-side;
 *   (d) `org_id`/`org_name` name the organization the token was issued for, and
 *       are the scope the role claims above are relative to.
 */

/** Build a user claim bag; `sub` is the only claim the SDK's type requires. */
function user(claims: Record<string, unknown>): WallowUser {
  return { sub: "user-1", ...claims };
}

describe("getRoles", () => {
  it("reads an array `roles` claim in claim order", () => {
    expect(getRoles(user({ roles: ["admin", "auditor"] }))).toEqual(["admin", "auditor"]);
  });

  it("treats a bare string `roles` claim as a single role", () => {
    expect(getRoles(user({ roles: "admin" }))).toEqual(["admin"]);
  });

  it("falls back to the singular `role` claim", () => {
    // An OIDC token carrying exactly one role may spell it either way.
    expect(getRoles(user({ role: "auditor" }))).toEqual(["auditor"]);
    expect(getRoles(user({ role: ["auditor", "admin"] }))).toEqual(["auditor", "admin"]);
  });

  it("prefers `roles` over `role` when both are present", () => {
    expect(getRoles(user({ roles: ["admin"], role: "auditor" }))).toEqual(["admin"]);
  });

  it("returns an empty array when the user carries no role claim", () => {
    expect(getRoles(user({}))).toEqual([]);
  });

  it("returns an empty array for a null or undefined user", () => {
    // `beforeLoad` guards call these before they know there is a session.
    expect(getRoles(null)).toEqual([]);
    expect(getRoles(undefined)).toEqual([]);
  });

  it("ignores non-string entries rather than coercing them", () => {
    // Coercion would turn a nested object into the role "[object Object]",
    // which `hasRole` could then be asked about with a straight face.
    expect(getRoles(user({ roles: ["admin", 42, null, { name: "admin" }, undefined] }))).toEqual([
      "admin",
    ]);
  });

  it("trims entries and drops blank ones", () => {
    expect(getRoles(user({ roles: ["  admin  ", "", "   "] }))).toEqual(["admin"]);
  });

  it("deduplicates case-insensitively, keeping the first spelling", () => {
    // Mirrors `GetRoles()`'s `Distinct(StringComparer.OrdinalIgnoreCase)`.
    expect(getRoles(user({ roles: ["Admin", "admin", "ADMIN", "auditor"] }))).toEqual([
      "Admin",
      "auditor",
    ]);
  });

  it("ignores a non-array, non-string role claim", () => {
    expect(getRoles(user({ roles: { name: "admin" } }))).toEqual([]);
  });
});

describe("hasRole", () => {
  it("matches case-insensitively", () => {
    const subject: WallowUser = user({ roles: ["Admin"] });

    expect(hasRole(subject, "admin")).toBe(true);
    expect(hasRole(subject, "ADMIN")).toBe(true);
  });

  it("matches against the singular `role` claim too", () => {
    expect(hasRole(user({ role: "auditor" }), "auditor")).toBe(true);
  });

  it("is false for a role the user does not hold", () => {
    expect(hasRole(user({ roles: ["auditor"] }), "admin")).toBe(false);
  });

  it("does not match on a prefix or substring", () => {
    // "administrator" is a different role, not a longer spelling of "admin".
    expect(hasRole(user({ roles: ["administrator"] }), "admin")).toBe(false);
    expect(hasRole(user({ roles: ["admin"] }), "administrator")).toBe(false);
  });

  it("trims the requested role", () => {
    expect(hasRole(user({ roles: ["admin"] }), "  admin ")).toBe(true);
  });

  it("is false for a blank requested role", () => {
    // Otherwise a blank claim entry and a blank question would agree.
    expect(hasRole(user({ roles: ["admin"] }), "")).toBe(false);
    expect(hasRole(user({ roles: ["admin"] }), "   ")).toBe(false);
  });

  it("is false for a null or undefined user", () => {
    expect(hasRole(null, "admin")).toBe(false);
    expect(hasRole(undefined, "admin")).toBe(false);
  });
});

describe("isAdmin", () => {
  it("is true when the user holds the admin role, whatever its casing", () => {
    expect(isAdmin(user({ roles: ["admin"] }))).toBe(true);
    expect(isAdmin(user({ roles: ["Admin", "auditor"] }))).toBe(true);
    expect(isAdmin(user({ role: "ADMIN" }))).toBe(true);
  });

  it("is false without the admin role", () => {
    expect(isAdmin(user({ roles: ["auditor"] }))).toBe(false);
    expect(isAdmin(user({}))).toBe(false);
    expect(isAdmin(null)).toBe(false);
  });

  it("is not granted by the global-admin claim", () => {
    // The API keeps global admin as a distinct, non-assignable CLAIM rather
    // than a role — `TenantResolutionMiddleware` gates on
    // `IsGlobalAdmin() || IsOperator()` separately from any role check. Ask
    // `isGlobalAdmin()` when that is the question.
    const globalAdmin: WallowUser = user({ is_global_admin: true, roles: ["auditor"] });

    expect(isAdmin(globalAdmin)).toBe(false);
    expect(isGlobalAdmin(globalAdmin)).toBe(true);
  });

  it("is not granted by the operator claim", () => {
    expect(isAdmin(user({ is_operator: true }))).toBe(false);
  });
});

describe("isOperator / isGlobalAdmin (boolean claims)", () => {
  const flags: readonly { claim: string; read: (u: WallowUser | null) => boolean }[] = [
    { claim: "is_operator", read: isOperator },
    { claim: "is_global_admin", read: isGlobalAdmin },
  ];

  it.each(flags)("$claim is true for a literal boolean true", ({ claim, read }) => {
    expect(read(user({ [claim]: true }))).toBe(true);
  });

  it.each(flags)('$claim is true for the string "true", any casing', ({ claim, read }) => {
    // `bool.TryParse` is case-insensitive and trims, and a JWT delivers the
    // flag as a string — so the browser mirror must accept the same spellings.
    expect(read(user({ [claim]: "true" }))).toBe(true);
    expect(read(user({ [claim]: "True" }))).toBe(true);
    expect(read(user({ [claim]: "TRUE" }))).toBe(true);
    expect(read(user({ [claim]: " true " }))).toBe(true);
  });

  it.each(flags)("$claim is false for anything else truthy-looking", ({ claim, read }) => {
    // Only a literal true grants it: `bool.TryParse("1")` is false, and so is
    // this. Presence alone must never be enough.
    for (const value of ["1", 1, "yes", "on", "TRUEISH", {}, []]) {
      expect(read(user({ [claim]: value }))).toBe(false);
    }
  });

  it.each(flags)("$claim is false when explicitly false or absent", ({ claim, read }) => {
    expect(read(user({ [claim]: false }))).toBe(false);
    expect(read(user({ [claim]: "false" }))).toBe(false);
    expect(read(user({}))).toBe(false);
    expect(read(null)).toBe(false);
  });

  it("does not confuse the two flags with each other", () => {
    expect(isOperator(user({ is_global_admin: true }))).toBe(false);
    expect(isGlobalAdmin(user({ is_operator: true }))).toBe(false);
  });
});

describe.each([
  { name: "getOrgId", claim: "org_id", read: getOrgId },
  { name: "getOrgName", claim: "org_name", read: getOrgName },
])("$name", ({ claim, read }) => {
  it("reads the claim, trimmed", () => {
    expect(read(user({ [claim]: "  Contoso  " }))).toBe("Contoso");
  });

  it("is null when the claim is absent, blank or unauthenticated", () => {
    expect(read(user({}))).toBeNull();
    expect(read(user({ [claim]: "   " }))).toBeNull();
    expect(read(null)).toBeNull();
    expect(read(undefined)).toBeNull();
  });

  it("is null for a non-string claim", () => {
    // The API emits both as single string claims; an array or number here means
    // the bag is not what it says it is, and a caller keying UI state off it
    // must get nothing rather than `"[object Object]"`.
    for (const value of [42, true, {}, ["a"], null]) {
      expect(read(user({ [claim]: value }))).toBeNull();
    }
  });
});

describe("organization scoping", () => {
  it("does not confuse the org claims with each other", () => {
    expect(getOrgId(user({ org_name: "Contoso" }))).toBeNull();
    expect(getOrgName(user({ org_id: "org-1" }))).toBeNull();
  });

  it("reports roles alongside the organization that granted them", () => {
    // One token per organization: the roles on this bag are the roles the user
    // holds in `org_id`, not everywhere.
    const member: WallowUser = user({ org_id: "org-1", roles: ["admin"] });

    expect(getOrgId(member)).toBe("org-1");
    expect(isAdmin(member)).toBe(true);
  });
});

describe("browser entry surface", () => {
  it.each([
    "getRoles",
    "hasRole",
    "isAdmin",
    "isOperator",
    "isGlobalAdmin",
    "getOrgId",
    "getOrgName",
  ])("exports %s from the package root", (name: string) => {
    // The AC is "exported from the SDK browser entry": apps import these from
    // `@bc-solutions-coder/sdk`, never from a deep internal path.
    expect(Object.keys(browserEntry)).toContain(name);
    expect((browserEntry as unknown as Record<string, unknown>)[name]).toBeDefined();
  });
});
