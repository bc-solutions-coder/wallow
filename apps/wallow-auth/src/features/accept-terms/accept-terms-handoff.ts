/**
 * The AcceptTerms gate's non-React layer: the bounce-back code→copy mapping and
 * the URL the accepted consent hands the browser to.
 *
 * ── WHAT THE HAND-OFF IS ─────────────────────────────────────────────────────
 *
 * `AccountController.external-login-callback` data-protects the external
 * identity into the **ExternalLoginState** cookie (HttpOnly, Secure,
 * SameSite=Lax, 10 min) and redirects to the gate.
 * `complete-external-registration` reads that cookie back, creates the user,
 * signs them in, deletes the cookie, and redirects to the validated returnUrl.
 *
 * The user's identity for that step therefore lives ENTIRELY in a cookie the
 * screen cannot read: it makes no request, relays no token, and its only job is
 * to build this URL. What it DOES owe is that its two query-string passengers
 * cannot break out of the query string — that is the `encodeURIComponent` below.
 *
 * ── NO `isSafeReturnUrl` GUARD (adjudicated SAFE) ─────────────────────────────
 *
 * `returnUrl` is NOT guarded here, deliberately.
 * `OpenIddictRedirectUriValidator.IsAllowedAsync` requires an ABSOLUTE URI and
 * then origin-allow-lists it; `external-login` refuses to start the flow unless
 * it passes and `external-login-callback` re-validates, so every `returnUrl`
 * arriving here is absolute and allow-listed. `isSafeReturnUrl` returns true
 * only for a RELATIVE single-'/' path. The two accept-sets are provably
 * disjoint: wiring the guard in would send every social sign-up to
 * `/error?reason=invalid_redirect_uri`.
 *
 * This is the `buildConnectLogoutUrl` precedent, not the `buildConsentSubmission`
 * one. The rule: guard where the CLIENT picks the destination; defer where the
 * SERVER does. Here the destination is a same-origin CONSTANT path,
 * `returnUrl` is inert cargo, and `complete-external-registration` re-validates
 * it against the same allow-list before any user is created.
 */

import { BASE_PATH } from "@shared/lib/base-path";

/** The endpoint the gate hands the browser to. */
const COMPLETE_REGISTRATION_PATH = "/v1/identity/auth/complete-external-registration";

/** The oracle's `_ =>` arm. */
const GENERIC_ERROR_MESSAGE = "An error occurred. Please try again.";

/**
 * The oracle's `Error switch`. A `ReadonlyMap` + `.get()`, never a `Record` +
 * bracket lookup (bd memory `attacker-supplied-query-key-lookups-use-map-not-
 * record`): `?error=toString` is a URL anyone can send a victim, and an object
 * literal would resolve `Object.prototype.toString` — a FUNCTION handed to the
 * renderer. A Map only ever sees the keys put in it.
 *
 * `session_expired` is not reachable from the wire — `complete-external-
 * registration` sends every session-expired path to
 * `/login?error=session_expired`, and the only redirect back to the gate is
 * `?error=terms_required`. The branch is kept because `?error=` is a query
 * string anyone can construct, so it deserves deliberate handling rather than
 * falling to the generic arm by accident.
 */
const ERROR_MESSAGES: ReadonlyMap<string, string> = new Map([
  ["terms_required", "You must accept the terms to continue."],
  ["session_expired", "Your session has expired. Please try signing in again."],
]);

/** The copy for a bounce-back reason code — see {@link ERROR_MESSAGES}. */
export function bounceBackMessage(code: string): string {
  return ERROR_MESSAGES.get(code) ?? GENERIC_ERROR_MESSAGE;
}

/**
 * The URL an accepted consent hands the browser to.
 *
 * Built against THIS origin, not the oracle's `ApiBaseUrl`: this app's
 * passthrough reverse proxy mounts `/v1/**` at the root, so going cross-origin
 * would drop the `SameSite=Lax` ExternalLoginState cookie that is the user's
 * whole identity here and the endpoint would bounce them to
 * `/login?error=session_expired`. It is the BASE PATH rather than the empty
 * string because under a based build this app's passthrough answers under that
 * prefix, and the site root belongs to a different app entirely.
 *
 * Only a NULLISH `returnUrl` falls back (bd memory
 * `returnurl-guard-refuse-dont-sanitize`); "/" fails the API's absolute-URI
 * check, so the endpoint substitutes authUrl — the fallback means "send me
 * home", and the API decides where home is.
 *
 * `clientId` is omitted entirely rather than sent blank: the endpoint reads a
 * present-but-empty id as an unknown client, whose allow list is the authUrl
 * origin alone, and would then refuse the very returnUrl the user is mid-journey
 * to. It arrives as `client_id` and leaves as `clientId`, the name the endpoint
 * binds.
 *
 * The encoding on both is load-bearing, not cosmetic. They are attacker-supplied
 * cargo in a URL built by concatenation: unencoded, either would smuggle a
 * second `acceptedTerms` in, and ASP.NET binds a duplicated `[FromQuery] bool`
 * key as "true,false", which fails to parse and lands on the `!acceptedTerms`
 * branch.
 */
export function completeRegistrationUrl(
  returnUrl: string | undefined,
  clientId: string | undefined,
): string {
  const encodedReturnUrl: string = encodeURIComponent(returnUrl ?? "/");
  const clientIdParam: string =
    clientId === undefined || clientId === "" ? "" : `&clientId=${encodeURIComponent(clientId)}`;

  return `${BASE_PATH}${COMPLETE_REGISTRATION_PATH}?acceptedTerms=true&returnUrl=${encodedReturnUrl}${clientIdParam}`;
}
