/**
 * Typed errors for the BFF tunnel.
 *
 * Upstream (.NET) failures are surfaced as RFC 7807 `application/problem+json`
 * payloads. {@link parseProblemDetails} turns any upstream response body — well
 * formed problem details, an unexpected JSON shape, or plain text/HTML — into a
 * single {@link WallowError} type so callers never have to branch on the wire
 * format. {@link redact} scrubs credential-shaped values before logging.
 */

/**
 * RFC 7807 problem details as emitted by the Wallow API.
 *
 * ASP.NET Core carries the machine-readable error code in `extensions.code`;
 * some serializer configurations flatten extension members onto the root
 * object, so both placements are tolerated by {@link parseProblemDetails}.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  extensions?: Record<string, unknown>;
  [member: string]: unknown;
}

/** Code used when the upstream response carries no machine-readable code. */
export const UNKNOWN_ERROR_CODE: string = "UNKNOWN";

/** Placeholder substituted for credential-shaped values by {@link redact}. */
export const REDACTED: string = "[redacted]";

/**
 * An error raised by the BFF for a failed upstream call.
 *
 * `status` is the HTTP status of the upstream response (or the status the BFF
 * synthesizes for network faults), `code` is the machine-readable error code,
 * and `title`/`detail` mirror the RFC 7807 members.
 */
export class WallowError extends Error {
  readonly status: number;
  readonly code: string;
  readonly title: string;
  readonly detail?: string;

  constructor(init: { status: number; code: string; title: string; detail?: string }) {
    super(init.detail ? `${init.title}: ${init.detail}` : init.title);
    this.name = "WallowError";
    this.status = init.status;
    this.code = init.code;
    this.title = init.title;
    this.detail = init.detail;
  }
}

/**
 * Parses an upstream response body into a {@link WallowError}.
 *
 * Falls back to a synthetic {@link UNKNOWN_ERROR_CODE} error when the body is
 * not JSON or is not shaped like problem details.
 */
export function parseProblemDetails(response: Response, bodyText: string): WallowError {
  const problem: ProblemDetails | undefined = tryParseProblem(bodyText);

  if (!problem) {
    return new WallowError({
      status: response.status,
      code: UNKNOWN_ERROR_CODE,
      title: UNKNOWN_ERROR_TITLE,
    });
  }

  return new WallowError({
    status: typeof problem.status === "number" ? problem.status : response.status,
    code: readCode(problem) ?? UNKNOWN_ERROR_CODE,
    title: typeof problem.title === "string" ? problem.title : UNKNOWN_ERROR_TITLE,
    detail: typeof problem.detail === "string" ? problem.detail : undefined,
  });
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

/** Title used when the upstream response carries no problem details. */
const UNKNOWN_ERROR_TITLE: string = "Unknown error";

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

function tryParseProblem(bodyText: string): ProblemDetails | undefined {
  if (!bodyText.trim()) {
    return undefined;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(bodyText);
  } catch {
    return undefined;
  }

  return isPlainObject(parsed) ? (parsed as ProblemDetails) : undefined;
}

function readCode(problem: ProblemDetails): string | undefined {
  const fromExtensions: unknown = problem.extensions?.["code"];
  if (typeof fromExtensions === "string") {
    return fromExtensions;
  }

  return typeof problem.code === "string" ? problem.code : undefined;
}

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
