/**
 * Typed errors for the BFF tunnel.
 *
 * Upstream (.NET) failures are surfaced as RFC 7807 `application/problem+json`
 * payloads. {@link parseProblemDetails} turns any upstream response body — well
 * formed problem details, an unexpected JSON shape, or plain text/HTML — into a
 * single {@link WallowError} type so callers never have to branch on the wire
 * format. {@link redact} scrubs credential-shaped values before logging.
 *
 * {@link WallowError} itself lives in `../errors`, shared with the browser
 * entry; it is re-exported here so this module stays the one place server code
 * imports errors from.
 */

import { WallowError } from "../errors";
import { REQUEST_ID_HEADER } from "../request-id";

export { isWallowError, WallowError } from "../errors";

/**
 * RFC 7807 problem details as emitted by the Wallow API.
 *
 * ASP.NET Core carries the machine-readable error code in `extensions.code`;
 * some serializer configurations flatten extension members onto the root
 * object, so both placements are tolerated by {@link parseProblemDetails}.
 *
 * The Identity auth endpoints are the exception: they return a bare
 * `{ succeeded: false, error: "invalid_code" }` object instead of problem
 * details, so `error` carries the code there. All three are probed.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  /** Machine-readable code on the non-problem-details auth endpoint bodies. */
  error?: string;
  /** Present on the auth endpoint bodies; always `false` on a failure. */
  succeeded?: boolean;
  /**
   * The W3C trace id ASP.NET Core stamps on every problem details body, and the
   * value OTel exports the request's trace under. Flattened here and repeated
   * inside {@link extensions} depending on the serializer configuration.
   */
  traceId?: string;
  /**
   * Validation messages keyed by property name, as ASP.NET Core's
   * `ValidationProblemDetails` (and FluentValidation behind it) emits them on a
   * 400. Surfaced on {@link WallowError.fieldErrors}.
   */
  errors?: Record<string, string[]>;
  extensions?: Record<string, unknown>;
  [member: string]: unknown;
}

/** Code used when the upstream response carries no machine-readable code. */
export const UNKNOWN_ERROR_CODE: string = "UNKNOWN";

/** Placeholder substituted for credential-shaped values by {@link redact}. */
export const REDACTED: string = "[redacted]";

/**
 * Parses an upstream response body into a {@link WallowError}.
 *
 * Falls back to a synthetic {@link UNKNOWN_ERROR_CODE} error when the body is
 * not JSON or is not shaped like problem details.
 *
 * The two correlation members arrive by different routes and both survive onto
 * the error even on that fallback path: `requestId` off the response's
 * {@link REQUEST_ID_HEADER}, `traceId` out of the body. A gateway's HTML error
 * page is precisely where they matter most, since it names nothing else the
 * request could be traced by.
 */
export function parseProblemDetails(response: Response, bodyText: string): WallowError {
  const requestId: string | undefined = response.headers.get(REQUEST_ID_HEADER) ?? undefined;
  const problem: ProblemDetails | undefined = tryParseProblem(bodyText);

  if (!problem) {
    return new WallowError({
      status: response.status,
      code: UNKNOWN_ERROR_CODE,
      title: UNKNOWN_ERROR_TITLE,
      requestId,
    });
  }

  return new WallowError({
    status: typeof problem.status === "number" ? problem.status : response.status,
    code: readCode(problem) ?? UNKNOWN_ERROR_CODE,
    title: typeof problem.title === "string" ? problem.title : UNKNOWN_ERROR_TITLE,
    detail: typeof problem.detail === "string" ? problem.detail : undefined,
    requestId,
    traceId: readTraceId(problem),
    fieldErrors: readFieldErrors(problem),
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

/** Surviving-entry count below which {@link readFieldErrors} reports nothing. */
const NO_FIELD_ERRORS: number = 0;

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

/**
 * Recover the machine-readable error code, probing the three placements the
 * Wallow API actually uses, most authoritative first: `extensions.code` (RFC
 * 7807 as ASP.NET Core emits it), `code` (the same, flattened), and finally
 * `error` — the Identity auth endpoints return a bare `{ succeeded, error }`
 * object rather than problem details. `error` is probed last so real problem
 * details always win, and non-string members are ignored rather than coerced.
 */
function readCode(problem: ProblemDetails): string | undefined {
  const fromExtensions: unknown = problem.extensions?.["code"];
  if (typeof fromExtensions === "string") {
    return fromExtensions;
  }

  if (typeof problem.code === "string") {
    return problem.code;
  }

  return typeof problem.error === "string" ? problem.error : undefined;
}

/**
 * Recover the backend's W3C trace id, preferring `extensions.traceId` over the
 * flattened top-level member exactly as {@link readCode} prefers
 * `extensions.code`: which of the two the API emits depends on its problem
 * details serializer configuration, and the extensions placement is the RFC
 * 7807 one. Non-string members are ignored rather than coerced.
 */
function readTraceId(problem: ProblemDetails): string | undefined {
  const fromExtensions: unknown = problem.extensions?.["traceId"];
  if (typeof fromExtensions === "string") {
    return fromExtensions;
  }

  return typeof problem.traceId === "string" ? problem.traceId : undefined;
}

/**
 * Recover the per-property validation messages, as ASP.NET Core's
 * `ValidationProblemDetails` emits them on a 400.
 *
 * Unlike `code` and `traceId` there is no `extensions` placement to probe:
 * `errors` is a declared member of `ValidationProblemDetails`, not an extension,
 * so a flattening serializer has nothing to move.
 *
 * The body is untrusted wire data that {@link tryParseProblem} only asserted the
 * type of, so every entry is validated rather than cast — an entry survives only
 * when its value is an array of strings. A body whose `errors` leaves no entry
 * standing yields `undefined` rather than an empty record, keeping "the API sent
 * no field errors" distinguishable from "the API sent field errors".
 */
function readFieldErrors(problem: ProblemDetails): Record<string, readonly string[]> | undefined {
  const errors: unknown = problem.errors;
  if (!isPlainObject(errors)) {
    return undefined;
  }

  const fieldErrors: Record<string, readonly string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (isStringArray(messages)) {
      fieldErrors[field] = messages;
    }
  }

  return Object.keys(fieldErrors).length > NO_FIELD_ERRORS ? fieldErrors : undefined;
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

function isStringArray(value: unknown): value is readonly string[] {
  return Array.isArray(value) && value.every((item: unknown) => typeof item === "string");
}
