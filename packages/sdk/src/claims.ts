/**
 * Browser-side claim helpers (Wallow-pu6a.5.6).
 *
 * Every app that gates UI on who the user is has, until now, re-derived the
 * answer from the raw claim bag by hand — wallow-web's `/dashboard` route
 * carried its own `isAdminUser()` that coped with `roles` vs `role` and
 * array-vs-string shapes inline. These helpers are that logic extracted once,
 * with the same semantics the API enforces server-side.
 *
 * The contracts mirror `Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions`:
 *
 *   - roles come from the `roles`/`role` claim and are compared
 *     case-insensitively, deduplicated the way `GetRoles()` deduplicates
 *     (`StringComparer.OrdinalIgnoreCase`, first spelling wins);
 *   - `is_operator` and `is_global_admin` are BOOLEAN claims, not roles, and
 *     only a literal true grants them — the browser mirror of `bool.TryParse`,
 *     so `"1"`/`"yes"`/absent never do.
 *
 * These are read-only conveniences over a claim bag the browser was handed;
 * they are NOT an authorization decision. The API re-checks every claim on
 * every request — a caller who forges `is_global_admin` in a devtools console
 * changes what this returns and nothing else.
 */
import type { WallowUser } from "./auth";

/** The tenant administrator role name, compared case-insensitively. */
const ADMIN_ROLE = "admin";

/** The API's platform-operator claim (`ClaimsPrincipalExtensions.OperatorClaimType`). */
const OPERATOR_CLAIM = "is_operator";

/** The API's global-administrator claim (`ClaimsPrincipalExtensions.GlobalAdminClaimType`). */
const GLOBAL_ADMIN_CLAIM = "is_global_admin";

/**
 * Read a boolean claim the way `bool.TryParse` reads it: the literal boolean
 * `true`, or a string that trims to `"true"` in any casing. Everything else —
 * including `1`, `"1"`, `"yes"` and mere presence — is false.
 */
function readBooleanClaim(user: WallowUser | null | undefined, claim: string): boolean {
  const value: unknown = user?.[claim];

  if (typeof value === "boolean") {
    return value;
  }

  return typeof value === "string" && value.trim().toLowerCase() === "true";
}

/**
 * All role names on the user's claim bag, in claim order.
 *
 * Reads `roles` first and falls back to the singular `role` claim (an OIDC
 * token with exactly one role may carry either). A bare string is treated as a
 * single role; non-string entries are ignored; entries are trimmed and blanks
 * dropped; duplicates that differ only by case collapse to the first spelling
 * seen.
 *
 * @param user The user, or `null`/`undefined` when unauthenticated.
 * @returns The role names, or an empty array when there are none.
 */
export function getRoles(user: WallowUser | null | undefined): readonly string[] {
  const claim: unknown = user?.roles ?? user?.role;

  let entries: readonly unknown[] = [];
  if (Array.isArray(claim)) {
    entries = claim;
  } else if (typeof claim === "string") {
    entries = [claim];
  }

  const roles: string[] = [];
  const seen = new Set<string>();

  for (const entry of entries) {
    if (typeof entry === "string") {
      const role: string = entry.trim();
      const key: string = role.toLowerCase();

      if (role !== "" && !seen.has(key)) {
        seen.add(key);
        roles.push(role);
      }
    }
  }

  return roles;
}

/**
 * Whether the user holds `role`, compared case-insensitively (both sides
 * trimmed), against {@link getRoles}.
 *
 * @param user The user, or `null`/`undefined` when unauthenticated.
 * @param role The role name to look for. A blank name is never held.
 */
export function hasRole(user: WallowUser | null | undefined, role: string): boolean {
  const wanted: string = role.trim().toLowerCase();
  if (wanted === "") {
    return false;
  }

  return getRoles(user).some((held: string) => held.toLowerCase() === wanted);
}

/**
 * Whether the user holds the tenant `admin` role.
 *
 * This is the ROLE check only. `is_global_admin` is deliberately not consulted:
 * the API keeps global admin as a distinct, non-assignable claim rather than a
 * role (`TenantResolutionMiddleware` gates on `IsGlobalAdmin() || IsOperator()`
 * SEPARATELY from any role), and collapsing the two here would quietly re-create
 * the role/claim conflation that was removed server-side. Ask
 * {@link isGlobalAdmin} when that is the question.
 *
 * @param user The user, or `null`/`undefined` when unauthenticated.
 */
export function isAdmin(user: WallowUser | null | undefined): boolean {
  return hasRole(user, ADMIN_ROLE);
}

/**
 * Whether the user carries the platform operator flag (`is_operator`), the
 * claim the API requires before honouring a cross-tenant `X-Tenant-Id`
 * override.
 *
 * Only a literal true grants it — the boolean `true` or a string that parses as
 * `"true"` case-insensitively, matching `bool.TryParse`. Absence or any other
 * value is false.
 *
 * @param user The user, or `null`/`undefined` when unauthenticated.
 */
export function isOperator(user: WallowUser | null | undefined): boolean {
  return readBooleanClaim(user, OPERATOR_CLAIM);
}

/**
 * Whether the user carries the global administrator flag (`is_global_admin`),
 * the API's cross-tenant governance escape hatch. Same literal-true parsing as
 * {@link isOperator}, and likewise never derived from a role.
 *
 * @param user The user, or `null`/`undefined` when unauthenticated.
 */
export function isGlobalAdmin(user: WallowUser | null | undefined): boolean {
  return readBooleanClaim(user, GLOBAL_ADMIN_CLAIM);
}
