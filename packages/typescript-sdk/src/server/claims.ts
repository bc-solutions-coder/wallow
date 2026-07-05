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
    throw new Error(
      "Malformed id_token: expected a JWT with a payload segment",
    );
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
