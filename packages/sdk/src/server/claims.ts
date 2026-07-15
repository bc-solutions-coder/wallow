/**
 * id_token claim decoding for the BFF tunnel.
 *
 * The BFF receives an `id_token` directly from the OIDC token endpoint over
 * TLS, so its payload can be trusted without local signature verification. This
 * module decodes the base64url-encoded JWT payload and projects the standard
 * OIDC claims into the {@link BffSession.user} shape.
 */

import type { BffSession } from "./session";

/**
 * Decode the payload of an OIDC `id_token` into the session user identity.
 *
 * The token is split on `.`; the payload segment is base64url-decoded and
 * JSON-parsed. `sub` is required (a non-string `sub` is treated as malformed).
 * `email` and `name` are copied through when present as strings, and any other
 * claims pass through via the {@link BffSession.user} index signature.
 *
 * @param idToken The raw `id_token` JWT string.
 * @returns The resolved session user claims.
 * @throws If the token is malformed or is missing a string `sub` claim.
 */
export function decodeIdTokenClaims(idToken: string): BffSession["user"] {
  const parts: string[] = idToken.split(".");
  if (parts.length < 2) {
    throw new Error("Malformed id_token: expected a JWT with a payload segment");
  }

  const decoded: string = Buffer.from(parts[1], "base64url").toString("utf8");
  const payload: unknown = JSON.parse(decoded);

  if (
    typeof payload !== "object" ||
    payload === null ||
    typeof (payload as Record<string, unknown>).sub !== "string"
  ) {
    throw new Error("Malformed id_token: missing string 'sub' claim");
  }

  const claims: Record<string, unknown> = payload as Record<string, unknown>;
  const user: BffSession["user"] = {
    ...claims,
    sub: claims.sub as string,
  };

  if (typeof claims.email === "string") {
    user.email = claims.email;
  } else {
    delete user.email;
  }

  if (typeof claims.name === "string") {
    user.name = claims.name;
  } else {
    delete user.name;
  }

  return user;
}

/**
 * Map a merged claims object (id_token payload overlaid with userinfo) into the
 * first-class {@link BffSession.user} fields.
 *
 * Normalizes authorization claims into arrays and lifts tenant claims into
 * dedicated fields: `role`/`roles` merge into `roles`, `permissions`/`scope`
 * merge into `permissions`, `tenant_id` becomes `tenantId`, `tenant_name`
 * becomes `tenantName`. All other claims (including `sub`, `email`, `name`) pass
 * through via the {@link BffSession.user} index signature.
 *
 * @param claims The merged OIDC claims object.
 * @returns The resolved session user with normalized first-class fields.
 */
export function mapClaims(claims: Record<string, unknown>): BffSession["user"] {
  const rest: Record<string, unknown> = { ...claims };
  delete rest.role;
  delete rest.roles;
  delete rest.permissions;
  delete rest.scope;
  delete rest.tenant_id;
  delete rest.tenant_name;

  const user: BffSession["user"] = {
    ...rest,
    sub: typeof claims.sub === "string" ? claims.sub : "",
  };

  const roles: string[] = dedupe([...asStringList(claims.role), ...asStringList(claims.roles)]);
  if (roles.length > 0) {
    user.roles = roles;
  }

  const permissions: string[] = dedupe([
    ...asStringList(claims.permissions),
    ...splitScope(claims.scope),
  ]);
  if (permissions.length > 0) {
    user.permissions = permissions;
  }

  if (typeof claims.tenant_id === "string") {
    user.tenantId = claims.tenant_id;
  }

  if (typeof claims.tenant_name === "string") {
    user.tenantName = claims.tenant_name;
  }

  return user;
}

/**
 * Normalize a claim value that may be a single string or an array of strings
 * into a string array, discarding non-string entries.
 */
function asStringList(value: unknown): string[] {
  if (typeof value === "string") {
    return [value];
  }
  if (Array.isArray(value)) {
    return value.filter((entry): entry is string => typeof entry === "string");
  }
  return [];
}

/** Split a space-delimited OAuth `scope` string into individual scopes. */
function splitScope(value: unknown): string[] {
  if (typeof value !== "string") {
    return [];
  }
  return value.split(" ").filter((scope: string): boolean => scope.length > 0);
}

/** Return a new array with duplicate entries removed, preserving order. */
function dedupe(values: string[]): string[] {
  return [...new Set(values)];
}
