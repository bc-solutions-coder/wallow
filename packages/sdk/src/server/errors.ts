/**
 * The BFF tunnel's own errors.
 *
 * Every failure the tunnel raises is an `ApiFailure` from
 * `@bc-solutions-coder/api-errors`: an upstream body is parsed by that
 * package's `failureFromResponse`, and the tunnel's own faults are built with
 * the same class. What lives here is the one failure the proxy answers with a
 * teardown ({@link RefreshFailedError}) and {@link redact}, which scrubs
 * credential-shaped values before logging.
 */

import { ApiFailure, ClientErrorCode } from "@bc-solutions-coder/api-errors";

/** Placeholder substituted for credential-shaped values by {@link redact}. */
export const REDACTED: string = "[redacted]";

/** HTTP status a refresh-failure teardown answers with. */
const UNAUTHORIZED_STATUS = 401;

/**
 * A session refresh that failed terminally: the grant behind the session was
 * rejected (revoked at the auth host by a logout elsewhere or a deactivation),
 * there was no refresh token left to spend, or the store record vanished
 * mid-refresh. Replaying the refresh on the next request could only fail the
 * same way, so the proxy answers it by tearing the session down — store record
 * destroyed, cookies cleared — rather than by a bare 401 the browser would
 * retry forever.
 */
export class RefreshFailedError extends ApiFailure {
  constructor(detail?: string) {
    super({
      status: UNAUTHORIZED_STATUS,
      code: ClientErrorCode.BFF_SESSION_REFRESH_FAILED,
      title: "The session could not be refreshed",
      detail,
    });
    this.name = "RefreshFailedError";
  }
}

/**
 * Returns a deep copy of `value` with credential-shaped members replaced by
 * {@link REDACTED}, safe for logging.
 */
export function redact(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((item: unknown) => redact(item));
  }

  if (isPlainObject(value)) {
    const copy: Record<string, unknown> = {};
    for (const [member, memberValue] of Object.entries(value)) {
      copy[member] = isSensitiveMember(member) ? REDACTED : redact(memberValue);
    }
    return copy;
  }

  if (typeof value === "string" && isTokenShaped(value)) {
    return REDACTED;
  }

  return value;
}

/** Member names whose values are always credentials, whatever they contain. */
const SENSITIVE_MEMBERS: ReadonlySet<string> = new Set([
  "authorization",
  "cookie",
  "set-cookie",
  "password",
]);

/** Member name fragments that mark a value as a credential. */
const SENSITIVE_MEMBER_FRAGMENTS: readonly string[] = ["token", "secret"];

/** `Bearer <credential>` and bare three-segment JWTs, wherever they appear. */
const BEARER_PREFIX: RegExp = /^bearer\s+\S/iu;
const JWT_SHAPE: RegExp = /^[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}$/u;

function isSensitiveMember(member: string): boolean {
  const normalized: string = member.toLowerCase();

  return (
    SENSITIVE_MEMBERS.has(normalized) ||
    SENSITIVE_MEMBER_FRAGMENTS.some((fragment: string) => normalized.includes(fragment))
  );
}

function isTokenShaped(value: string): boolean {
  return BEARER_PREFIX.test(value) || JWT_SHAPE.test(value);
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
