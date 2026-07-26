/**
 * MFA error surfacing (Wallow-8w1h.6.6). The MFA endpoints
 * (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`)
 * return ALL business failures as a raw anonymous object
 * `{ succeeded: false, error: "<code>" }` via `BadRequest`/`Unauthorized` — NOT
 * an RFC 7807 ProblemDetails body. The SDK's `unwrap()`
 * (`src/lib/wallow-sdk.ts`) THROWS that raw body on any non-2xx response, so a
 * component's `onError` receives `{ succeeded: false, error }` with NO `.detail`.
 *
 * `mapMfaError` turns that machine `error` code into a friendly, user-facing
 * message; `problemDetail` layers it under an RFC 7807 `detail` (for endpoints
 * that DO produce ProblemDetails) and above a step-specific fallback.
 */
import type { ProblemDetails } from "@bc-solutions-coder/sdk";

/** Friendly, user-facing copy per known MFA machine error code. */
const MFA_ERROR_MESSAGES: Record<string, string> = {
  no_auth_session: "Your session has expired. Please sign in again.",
  invalid_password: "That password is incorrect.",
  invalid_code: "That verification code is not valid.",
};

/**
 * Map an MFA machine error code to a friendly message. A known code returns its
 * mapped copy; an unmapped code falls back to the raw code; a missing code
 * returns `undefined` so callers can defer to their own fallback.
 */
export function mapMfaError(code: string | undefined | null): string | undefined {
  if (code === undefined || code === null || code === "") {
    return undefined;
  }
  return MFA_ERROR_MESSAGES[code] ?? code;
}

/**
 * Resolve a thrown MFA error to display text: an RFC 7807 `detail` when present,
 * else the mapped `{ error }` code from the raw controller body, else `fallback`.
 */
export function problemDetail(error: unknown, fallback: string): string {
  return (
    (error as ProblemDetails | null)?.detail ??
    mapMfaError((error as { error?: string } | null)?.error) ??
    fallback
  );
}
