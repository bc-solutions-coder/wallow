/**
 * OIDC Back-Channel Logout 1.0, RP side: the OP POSTs a signed logout token to
 * this handler when the SSO session ends, and the BFF destroys the matching
 * server-side session(s) with zero consumer code.
 *
 * The caller is the OP, not a browser, so no cookie is read and no CSRF gate
 * applies — the security of the endpoint is the logout token itself: signature
 * against the issuer's JWKS, `iss`/`aud`/`iat`/`exp`, plus the spec's hand
 * checks (§2.6). Every response carries `cache-control: no-store`, and an
 * invalid token is answered with an undifferentiated `400 {"error":
 * "invalid_request"}` so nothing about the session population leaks.
 */

import { createRemoteJWKSet, jwtVerify, type JWTPayload, type JWTVerifyResult } from "jose";
import { tokenRevocation, type Configuration } from "openid-client";

import type { BffConfig } from "./config";
import type { BffHandler } from "./handlers";
import { discover, type DiscoveryDoc } from "./oidc";
import { type BffSession } from "./session";
import type { SessionStore } from "./store/types";

/** The event URI a logout token's `events` claim must carry (§2.4). */
const LOGOUT_EVENT = "http://schemas.openid.net/event/backchannel-logout";

/**
 * The `typ` values accepted for a logout token. A `typ` header SHOULD be
 * `logout+jwt` (§2.4) — enforced when present, tolerated when absent, so OPs
 * predating that SHOULD keep working while a mis-sent id or access token
 * (typed `JWT`/`at+jwt`) is rejected outright.
 */
const LOGOUT_TOKEN_TYPES: ReadonlySet<string> = new Set(["logout+jwt", "application/logout+jwt"]);

/**
 * Asymmetric signature algorithms accepted for logout tokens. An explicit
 * allowlist so a symmetric `HS*` token — which any party knowing the client
 * secret could mint — can never pass as an OP-signed logout instruction.
 */
const ALLOWED_ALGORITHMS: readonly string[] = [
  "RS256",
  "RS384",
  "RS512",
  "PS256",
  "PS384",
  "PS512",
  "ES256",
  "ES384",
  "ES512",
  "EdDSA",
];

/** Accepted clock skew between the OP and this host (seconds). */
const CLOCK_TOLERANCE_SECONDS = 30;

const OK_STATUS = 200;
const BAD_REQUEST_STATUS = 400;
const METHOD_NOT_ALLOWED_STATUS = 405;

/** Only ever POSTed to (§2.5); everything else is answered 405. */
const BACKCHANNEL_METHOD = "POST";

/** Logout responses must never be cached (§2.8). */
const NO_STORE_HEADERS: Readonly<Record<string, string>> = { "cache-control": "no-store" };

/**
 * Remote JWKS resolvers keyed by JWKS URL. `createRemoteJWKSet` caches the
 * fetched keys and handles rotation cooldowns internally, so one resolver per
 * URL for the process lifetime is the intended usage.
 */
const jwksCache = new Map<string, ReturnType<typeof createRemoteJWKSet>>();

function jwksFor(jwksUri: string): ReturnType<typeof createRemoteJWKSet> {
  let jwks: ReturnType<typeof createRemoteJWKSet> | undefined = jwksCache.get(jwksUri);
  if (jwks === undefined) {
    jwks = createRemoteJWKSet(new URL(jwksUri));
    jwksCache.set(jwksUri, jwks);
  }
  return jwks;
}

/**
 * Both spellings of the configured issuer. OpenIddict (and .NET generally)
 * mints `iss` from `Uri.AbsoluteUri`, which keeps a trailing slash a plain
 * origin-only issuer string would not carry, so both forms must verify.
 */
function issuerForms(issuer: string): string[] {
  const base: string = issuer.replace(/\/+$/u, "");
  return [base, `${base}/`];
}

/** The uniform rejection: nothing about WHY the token failed leaks out. */
function invalidRequest(): Response {
  return Response.json(
    { error: "invalid_request" },
    { status: BAD_REQUEST_STATUS, headers: NO_STORE_HEADERS },
  );
}

/**
 * Verify a logout token per Back-Channel Logout 1.0 §2.6 and return its
 * payload, or `null` when any check fails.
 */
async function verifyLogoutToken(
  token: string,
  jwksUri: string,
  config: BffConfig,
): Promise<JWTPayload | null> {
  let result: JWTVerifyResult;
  try {
    result = await jwtVerify(token, jwksFor(jwksUri), {
      issuer: issuerForms(config.issuer),
      audience: config.clientId,
      algorithms: [...ALLOWED_ALGORITHMS],
      clockTolerance: CLOCK_TOLERANCE_SECONDS,
      requiredClaims: ["iat", "exp", "jti", "events"],
    });
  } catch {
    return null;
  }

  const { payload, protectedHeader } = result;

  if (protectedHeader.typ !== undefined && !LOGOUT_TOKEN_TYPES.has(protectedHeader.typ)) {
    return null;
  }
  // The events claim must carry the logout event with an object value.
  const events: unknown = payload.events;
  if (typeof events !== "object" || events === null) {
    return null;
  }
  const eventValue: unknown = (events as Record<string, unknown>)[LOGOUT_EVENT];
  if (typeof eventValue !== "object" || eventValue === null) {
    return null;
  }
  // A nonce is what distinguishes an id token from a logout token: its
  // presence marks a replayed id token and is a hard rejection.
  if (payload.nonce !== undefined) {
    return null;
  }
  if (typeof payload.sid !== "string" && typeof payload.sub !== "string") {
    return null;
  }
  return payload;
}

/**
 * Destroy the sessions the token names: by `sid` when the token carries one
 * and the store can, else by `sub`. A store that can do neither is a no-op —
 * the server preset warned about that combination at boot.
 */
function revokeSessions(store: SessionStore, payload: JWTPayload): Promise<BffSession[]> {
  if (typeof payload.sid === "string" && store.revokeBySid !== undefined) {
    return store.revokeBySid(payload.sid);
  }
  if (typeof payload.sub === "string" && store.revokeBySubject !== undefined) {
    return store.revokeBySubject(payload.sub);
  }
  return Promise.resolve([]);
}

/**
 * Best-effort RFC 7009: revoke each destroyed session's refresh token
 * upstream so the token family dies with the session. Failures are swallowed
 * — the local session is already gone, and the OP ending the SSO session is
 * usually about to invalidate the grant on its own side anyway.
 */
async function revokeUpstreamTokens(doc: DiscoveryDoc, revoked: BffSession[]): Promise<void> {
  const configuration: Configuration | undefined = doc.configuration;
  if (configuration === undefined) {
    return;
  }
  await Promise.all(
    revoked.map(async (session: BffSession): Promise<void> => {
      if (session.refreshToken === undefined) {
        return;
      }
      try {
        await tokenRevocation(configuration, session.refreshToken, {
          token_type_hint: "refresh_token",
        });
      } catch {
        // Local revocation already happened; upstream cleanup is advisory.
      }
    }),
  );
}

/**
 * Build the `POST /bff/backchannel-logout` handler bound to a configuration
 * and session store.
 *
 * @param config Server-side BFF configuration naming the issuer and client id
 *   the logout token must be issued by and for.
 * @param store Session store whose optional `revokeBySid`/`revokeBySubject`
 *   perform the actual teardown.
 */
export function createBackchannelLogoutHandler(config: BffConfig, store: SessionStore): BffHandler {
  return async (request: Request): Promise<Response> => {
    if (request.method.toUpperCase() !== BACKCHANNEL_METHOD) {
      return new Response(null, {
        status: METHOD_NOT_ALLOWED_STATUS,
        headers: { ...NO_STORE_HEADERS, allow: BACKCHANNEL_METHOD },
      });
    }

    // `logout_token=<jwt>`, form-encoded (§2.5).
    const body: string = await request.text();
    const token: string | null = new URLSearchParams(body).get("logout_token");
    if (token === null || token === "") {
      return invalidRequest();
    }

    const doc: DiscoveryDoc = await discover(config);
    if (doc.jwks_uri === undefined || doc.jwks_uri === "") {
      // Without a JWKS nothing can be verified, and an unverifiable logout
      // instruction must not tear anything down.
      return invalidRequest();
    }

    const payload: JWTPayload | null = await verifyLogoutToken(token, doc.jwks_uri, config);
    if (payload === null) {
      return invalidRequest();
    }

    const revoked: BffSession[] = await revokeSessions(store, payload);
    await revokeUpstreamTokens(doc, revoked);

    // Success — including already-gone: an unknown sid or sub answers 200 too.
    return new Response(null, { status: OK_STATUS, headers: NO_STORE_HEADERS });
  };
}
