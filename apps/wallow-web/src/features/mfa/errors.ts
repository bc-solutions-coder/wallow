/**
 * MFA error surfacing. The MFA endpoints
 * (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/MfaController.cs`)
 * still answer business failures with a raw anonymous object
 * `{ succeeded: false, error: "<token>" }` via `BadRequest`/`Unauthorized` — NOT
 * an RFC 7807 problem. Moving them onto problems, and this table onto a
 * `defineFailureMessages` registry, is follow-up work on the failure model.
 *
 * That difference no longer reaches a component. The SDK's error interceptor
 * normalizes every failure — problem and raw shape alike — into an `ApiFailure`
 * through `@bc-solutions-coder/api-errors`. A raw body is parsed under the OAuth
 * grammar: `code` becomes `OAuth.<Token>` and `title` keeps the raw token, so
 * the token this table is keyed on is read off `title`. `mapMfaError` turns it
 * into friendly copy; `problemDetail` layers `detail` above it, and every other
 * failure (a transport fault, a real problem) takes the package's resolved
 * sentence, with a step-specific fallback beneath.
 */
import {
  type ApiFailure,
  ClientErrorCode,
  isApiFailure,
  resolveFailureMessage,
} from "@bc-solutions-coder/api-errors";

/** The code prefix the parser gives a bare `{ error: "<token>" }` body. */
const OAUTH_GRAMMAR_PREFIX: string = "OAuth.";

/** Friendly, user-facing copy per known MFA machine error token. */
const MFA_ERROR_MESSAGES: Record<string, string> = {
  no_auth_session: "Your session has expired. Please sign in again.",
  invalid_password: "That password is incorrect.",
  invalid_code: "That verification code is not valid.",
};

/**
 * Map an MFA machine error token to a friendly message. A known token returns
 * its mapped copy; an unmapped one falls back to the raw token; a missing one
 * returns `undefined` so callers can defer to their own fallback.
 */
export function mapMfaError(code: string | undefined | null): string | undefined {
  if (code === undefined || code === null || code === "") {
    return undefined;
  }
  return MFA_ERROR_MESSAGES[code] ?? code;
}

/**
 * The raw `error` token the MFA controllers sent, kept as `title` by the OAuth
 * grammar; `undefined` for every failure that did not come from that body.
 */
function rawToken(failure: ApiFailure): string | undefined {
  return failure.code.startsWith(OAUTH_GRAMMAR_PREFIX) ? failure.title : undefined;
}

/**
 * Resolve a thrown MFA error to display text: the `ApiFailure` detail when the
 * endpoint produced one, else the mapped raw token, else the package's sentence
 * for the code or status, else `fallback`. The parser's unrecognized-response
 * placeholder is an internal marker, not copy, so it takes `fallback` outright.
 *
 * The brand check is the gate: anything that did not come through the
 * interceptor contributes nothing, so an unbranded object cannot dictate
 * user-facing copy by merely carrying a `detail` or `error` member.
 */
export function problemDetail(error: unknown, fallback: string): string {
  if (!isApiFailure(error)) {
    return fallback;
  }
  if (error.detail !== undefined) {
    return error.detail;
  }
  if (error.code === ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE) {
    return fallback;
  }
  const token: string | undefined = rawToken(error);
  return token === undefined
    ? resolveFailureMessage(error, { fallback })
    : (mapMfaError(token) ?? fallback);
}
