/**
 * MFA error surfacing (Wallow-8w1h.6.6, rewired by Wallow-pu6a.5.3). The MFA
 * endpoints
 * (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`)
 * answer ALL business failures with a raw anonymous object
 * `{ succeeded: false, error: "<code>" }` via `BadRequest`/`Unauthorized` — NOT
 * an RFC 7807 ProblemDetails body.
 *
 * That difference no longer reaches this module. The SDK's error interceptor
 * normalizes every failure — problem details and raw shape alike — into a
 * `WallowError` before it leaves the client, so a component's `onError` receives
 * the machine code as `error.code` and the human sentence (when the endpoint
 * produced one) as `error.detail`. `mapMfaError` turns the code into friendly
 * copy; `problemDetail` layers `detail` above it and a step-specific fallback
 * below.
 */
import { isWallowError } from "@bc-solutions-coder/sdk";

/**
 * The interceptor's placeholder for a failure the API named no code for. It is
 * an internal marker, so it must never be shown to a user as if it were copy.
 */
const UNKNOWN_ERROR_CODE: string = "UNKNOWN";

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
 * Resolve a thrown MFA error to display text: the `WallowError` detail when the
 * endpoint produced one, else its mapped machine code, else `fallback`.
 *
 * The brand check is the gate: anything that did not come through the
 * interceptor contributes nothing, so an unbranded object cannot dictate
 * user-facing copy by merely carrying a `detail` or `error` member.
 */
export function problemDetail(error: unknown, fallback: string): string {
  if (!isWallowError(error)) {
    return fallback;
  }
  const code: string | undefined = error.code === UNKNOWN_ERROR_CODE ? undefined : error.code;
  return error.detail ?? mapMfaError(code) ?? fallback;
}
