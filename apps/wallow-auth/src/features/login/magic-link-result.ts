/**
 * The magic-link tab's RESULT LAYER (Wallow-vec7.3.12 / 2.8b): everything that
 * turns an untyped `auth.sendMagicLink` / `auth.verifyMagicLink` response into
 * user-facing copy, with no React and no SDK in it.
 *
 * WHY A SEPARATE MODULE FROM `./auth-result`. The oracle keeps ONE
 * `HandleSuccessfulAuth` and a DIFFERENT error switch per tab
 * (`api/src/Wallow.Auth/Components/Pages/Login.razor`). Wallow-vec7.3.11 split it
 * the same way: the shared branch table lives once in `./auth-result`, and each
 * panel owns its own error switch. This is the magic-link tab's switch. It stays
 * out of `auth-result.ts` so `.3.13` can add `otp-result.ts` beside it without
 * two beads editing the same function bodies, and it IMPORTS the shared copy
 * rather than restating it.
 *
 * ── THE WIRE, READ FROM THE CONTROLLER (not from a client DTO) ───────────────
 *
 * `AccountController` (api/.../Identity/Wallow.Identity.Api/Controllers/AccountController.cs):
 *
 *   POST /v1/identity/auth/passwordless/magic-link                        :824
 *     200 { succeeded: true }                                             :835
 *     400 { succeeded: false, error: "Rate limit exceeded. Please try again later." }
 *
 *   GET  /v1/identity/auth/passwordless/magic-link/verify                 :838
 *     200 { succeeded: true, email, signInTicket }                        :848
 *     401 { succeeded: false, error: "Invalid token format." }            PasswordlessService.cs:95
 *     401 { succeeded: false, error: "Invalid token." }                   PasswordlessService.cs:105
 *     401 { succeeded: false, error: "Token expired or already used." }   PasswordlessService.cs:112
 *
 * Unlike `auth.login` — where three of four outcomes ride inside a 200 — EVERY
 * failure here is a non-2xx, so `unwrap()` throws and the oracle's `else` arms are
 * reached through a REJECTION. As of Wallow-vec7.7 `readCode` probes
 * `extensions.code > code > error`, so the `error` member of the bare
 * `{ succeeded, error }` body arrives as `WallowError.code`.
 *
 * ── THE TOKENS ARE ENGLISH SENTENCES, AND THAT CHANGES NOTHING ───────────────
 *
 * A server-authored sentence is still a MACHINE TOKEN: it is matched against and
 * never rendered. It is tempting to just show it — that temptation is exactly the
 * oracle's `_ => result.Error` leak, which this port does not reproduce. The copy
 * below is the screen's own.
 */

import { GENERIC_MESSAGE, readMember, UNREACHABLE_MESSAGE } from "./auth-result";

/** The oracle's blank-input guard (Login.razor:376) — note WHITEspace. */
export const BLANK_EMAIL_MESSAGE = "Please enter your email.";

/** The oracle's `_magicLinkSent` alert (Login.razor:111). */
export const MAGIC_LINK_SENT_MESSAGE = "Check your email for a magic link.";

/**
 * DIVERGENCE (disclosed on the bead). The oracle shows its GENERIC copy for every
 * send failure — but the rate limit is the ONLY failure the service can produce
 * (`PasswordlessService.SendMagicLinkAsync` :56-60; an address with no account
 * returns SUCCESS, to defeat enumeration). "An error occurred. Please try again."
 * therefore tells a rate-limited user to do the one thing guaranteed not to work.
 *
 * This is the same call `.3.11` made keeping a 423 status fallback under
 * `loginFailureMessage` ("a locked-out user must not be told to retype their
 * password"), for the same reason.
 */
export const MAGIC_LINK_RATE_LIMITED_MESSAGE =
  "Too many sign-in link requests. Please wait a few minutes and try again.";

/** The oracle's `HandleVerifyMagicLink` switch (Login.razor:419). */
export const MAGIC_LINK_EXPIRED_MESSAGE =
  "This magic link has expired or has already been used. Please request a new one.";

/** The oracle's `_ =>` tail on the same switch (Login.razor:420). */
export const MAGIC_LINK_VERIFY_FAILED_MESSAGE =
  "An error occurred verifying the magic link. Please try again.";

/** `SendMagicLinkAsync`'s only failure token (PasswordlessService.cs:59). */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";

/**
 * The verify tokens that mean "this link is spent — get a new one", as opposed to
 * "something else went wrong".
 *
 * `"invalid_token"`, which the ORACLE names here, is DEAD: `ValidateMagicLinkAsync`
 * never returns it. Its live spelling is `"Invalid token."` (PasswordlessService.cs:105
 * — a failed HMAC comparison, i.e. a tampered or truncated link). The dead literal
 * is not ported and the live one the author plainly meant is mapped in its place.
 *
 * `"Invalid token format."` (:95) is deliberately NOT in this set: it rides the same
 * 401 and is what BINDS this map against a blanket `401 -> expired` rule.
 *
 * A `ReadonlySet`, not a `Record` — the same habit as `auth-result`'s error-param
 * `ReadonlyMap` (bd memory `attacker-supplied-query-key-lookups-use-map-not-record`).
 */
const SPENT_TOKENS: ReadonlySet<string> = new Set([
  "Token expired or already used.",
  "Invalid token.",
]);

/**
 * Did the API actually accept the send? The facade types this `Promise<unknown>`
 * (the C# endpoint returns an anonymous `Ok(new { … })` with no OpenAPI schema), so
 * the narrowing belongs here, at the boundary (bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`).
 *
 * STRICT `=== true`, reproducing C#'s `if (result.Succeeded)`: JS truthiness would
 * accept the string `"false"`. A body this screen cannot read is NOT a sent link —
 * telling a user to go check an inbox that will stay empty is worse than an error.
 */
export function magicLinkWasSent(body: unknown): boolean {
  return readMember(body, "succeeded") === true;
}

/**
 * The oracle's `HandleSendMagicLink` failure arms (Login.razor:388-396), reached
 * through a REJECTION rather than an `else` — see the module header.
 */
export function sendMagicLinkFailureMessage(cause: unknown): string {
  if (readMember(cause, "code") === RATE_LIMITED_TOKEN) {
    return MAGIC_LINK_RATE_LIMITED_MESSAGE;
  }

  // A network-level rejection carries NEITHER `code` NOR `status`, and that absence
  // is exactly what identifies it: the TS shape of the oracle's
  // `catch (HttpRequestException)`, which it keeps DISTINCT from its generic tail.
  if (readMember(cause, "status") === undefined) {
    return UNREACHABLE_MESSAGE;
  }

  return GENERIC_MESSAGE;
}

/**
 * The oracle's `HandleVerifyMagicLink` switch (Login.razor:417-421).
 *
 * NO STATUS FALLBACK, unlike `loginFailureMessage`. The Wallow-vec7.7 rule keeps one
 * "only where status identifies a failure alone" — and 401 does not identify one
 * here: it carries three tokens with TWO meanings. That is also why code-keying is
 * observable on this endpoint at all (`.3.11` could not bind it on `login`, where
 * each failure status carries exactly one token).
 */
export function verifyMagicLinkFailureMessage(cause: unknown): string {
  const code: unknown = readMember(cause, "code");

  if (typeof code === "string" && SPENT_TOKENS.has(code)) {
    return MAGIC_LINK_EXPIRED_MESSAGE;
  }

  if (readMember(cause, "status") === undefined) {
    return UNREACHABLE_MESSAGE;
  }

  return MAGIC_LINK_VERIFY_FAILED_MESSAGE;
}
