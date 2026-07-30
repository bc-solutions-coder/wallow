/**
 * Display text for a failed SDK call (Wallow-pu6a.5.5).
 *
 * Every generated operation rejects with a `WallowError` built by the SDK's
 * error interceptor, so the RFC 7807 `detail` the API sent is already parsed
 * onto the error — no component needs to re-parse a ProblemDetails body, and
 * none may cast a rejection to one (the `as ProblemDetails` casts the
 * hand-written query layer forced are what this replaces).
 *
 * The brand check is the gate: anything that did not come through the
 * interceptor contributes no copy of its own, so an arbitrary object cannot
 * dictate user-facing text by merely carrying a `detail` member.
 *
 * `features/mfa/errors.ts` keeps its own richer `problemDetail` — the MFA
 * endpoints answer with machine codes rather than sentences, so that vertical
 * needs a code-to-copy map this generic helper deliberately has no opinion on.
 */
import { isWallowError } from "@bc-solutions-coder/sdk";

/**
 * The human-readable sentence for `error`: the API's ProblemDetails `detail`
 * when it sent one, else the error's own message, else `fallback`.
 */
export function errorText(error: unknown, fallback: string): string {
  if (isWallowError(error)) {
    return error.detail ?? error.message;
  }
  return error instanceof Error && error.message !== "" ? error.message : fallback;
}
