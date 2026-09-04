/**
 * The two parsers: {@link failureFromResponse} for a response and its body
 * text, {@link toApiFailure} for whatever a call threw or resolved with.
 *
 * Both classify rather than describe: raw transport text and an unrecognised
 * body go on `cause`, never on `detail`, so nothing a person is shown was
 * written by a gateway or a stack trace.
 */

import { ClientErrorCode } from "./codes";
import { ApiFailure, isApiFailure } from "./failure";

/**
 * The header the BFF tunnel stamps its request id in (the API itself echoes a
 * correlation id under another name). Declared locally: no SDK import.
 */
const REQUEST_ID_HEADER: string = "x-request-id";
const RETRY_AFTER_HEADER: string = "retry-after";

const SERVICE_UNAVAILABLE: number = 503;
const GATEWAY_TIMEOUT: number = 504;
/** nginx's status for a client that closed the connection; no IANA name. */
const CLIENT_CLOSED_REQUEST: number = 499;
const INTERNAL_SERVER_ERROR: number = 500;

/** How many `cause` links the timeout classifier follows: a cycle or a long chain ends here. */
const MAX_CAUSE_DEPTH: number = 8;

const MILLISECONDS_PER_SECOND: number = 1000;
const NO_WAIT: number = 0;
const NO_FIELD_ERRORS: number = 0;
const FIRST_CHARACTER: number = 0;
const REST_OF_WORD: number = 1;

const UNRECOGNIZED_TITLE: string = "Unrecognized response";
const UNTITLED_PROBLEM_TITLE: string = "Request failed";

/** Undici's timeout codes, plus the socket-level one they wrap. */
const TIMEOUT_CODES: ReadonlySet<string> = new Set([
  "UND_ERR_CONNECT_TIMEOUT",
  "UND_ERR_HEADERS_TIMEOUT",
  "UND_ERR_BODY_TIMEOUT",
  "ETIMEDOUT",
]);

/** A non-negative integer with nothing else around it. */
const DELTA_SECONDS: RegExp = /^\d+$/u;

/**
 * RFC 9110's IMF-fixdate, the one date form a server may emit. `Date.parse`
 * alone is too lenient (it reads `-5` as a year), so the shape is checked first.
 */
const HTTP_DATE: RegExp = /^[A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$/u;

/** The OAuth `error` token grammar: lowercase words joined by underscores. */
const OAUTH_TOKEN_SEPARATOR: RegExp = /_+/u;

/**
 * What {@link failureFromResponse} reads off a response. A `Response` satisfies
 * it on every runtime; a test can hand over a literal.
 */
export interface FailureResponse {
  readonly status: number;
  readonly headers: { readonly get: (name: string) => string | null };
}

/** What a caller knows about a failure that the input itself may not carry. */
export interface FailureContext {
  /** The response status, when the input is a body or an error read off one. */
  readonly status?: number | undefined;
  /** The request id, when the caller read the header itself. */
  readonly requestId?: string | undefined;
  /** Seconds to wait, when the caller read `Retry-After` itself (see {@link parseRetryAfter}). */
  readonly retryAfter?: number | undefined;
}

/**
 * Builds the failure for a non-2xx response whose body has been read.
 *
 * A problem body (an object with a string `code`) is parsed as problem+json
 * with the code at the top level; an OAuth body (`{ error, error_description }`)
 * is normalised to `OAuth.<PascalCase>`; anything else is
 * `Client.UnrecognizedResponse` at the response status with the body text on
 * `cause`. The request id and `Retry-After` headers ride along on all three.
 */
export function failureFromResponse(response: FailureResponse, bodyText: string): ApiFailure {
  const requestId: string | undefined = response.headers.get(REQUEST_ID_HEADER) ?? undefined;
  const retryAfter: number | undefined = parseRetryAfter(response.headers.get(RETRY_AFTER_HEADER));
  const body: unknown = tryParseJson(bodyText);

  return fromBody(body, bodyText, { status: response.status, requestId, retryAfter });
}

/**
 * Builds the failure for whatever a call threw or resolved with.
 *
 * - An {@link ApiFailure} passes through untouched.
 * - A thrown error with no status in `context` is a transport fault:
 *   `Transport.Timeout` (a `TimeoutError`, or an undici timeout code on the
 *   error or its cause), `Transport.Aborted` (an `AbortError`), else
 *   `Transport.NetworkError`.
 * - A plain object is read as a body: problem+json, an OAuth error, or nothing
 *   recognisable.
 * - Anything else, and a thrown error that does have a status, is
 *   `Client.UnrecognizedResponse` at `context.status` (500 when unknown) with
 *   the input on `cause`.
 */
export function toApiFailure(input: unknown, context: FailureContext = {}): ApiFailure {
  if (isApiFailure(input)) {
    return input;
  }

  if (input instanceof Error) {
    return context.status === undefined
      ? transportFailure(input, context.requestId)
      : unrecognized(input, {
          status: context.status,
          requestId: context.requestId,
          retryAfter: context.retryAfter,
        });
  }

  return fromBody(input, input, {
    status: context.status,
    requestId: context.requestId,
    retryAfter: context.retryAfter,
  });
}

interface ResponseFacts {
  /** The HTTP status, when a response answered; a bare body has none. */
  readonly status: number | undefined;
  readonly requestId: string | undefined;
  readonly retryAfter: number | undefined;
}

function fromBody(body: unknown, raw: unknown, facts: ResponseFacts): ApiFailure {
  if (!isPlainObject(body)) {
    return unrecognized(raw, facts);
  }

  if (typeof body["code"] === "string") {
    return problemFailure(body, body["code"], facts);
  }

  if (typeof body["error"] === "string") {
    return oauthFailure(body, body["error"], facts);
  }

  return unrecognized(raw, facts);
}

/**
 * The body's `status` is advisory: it only stands in when no response answered
 * (a bare object handed to `toApiFailure`). When one did, what HTTP said is what
 * the surfaces branch on, so a relayed 502 carrying a stale 400 body stays a 502.
 */
function readBodyStatus(problem: Record<string, unknown>): number | undefined {
  return typeof problem["status"] === "number" ? problem["status"] : undefined;
}

function problemFailure(
  problem: Record<string, unknown>,
  code: string,
  facts: ResponseFacts,
): ApiFailure {
  return new ApiFailure({
    status: facts.status ?? readBodyStatus(problem) ?? INTERNAL_SERVER_ERROR,
    code,
    title: typeof problem["title"] === "string" ? problem["title"] : UNTITLED_PROBLEM_TITLE,
    detail: typeof problem["detail"] === "string" ? problem["detail"] : undefined,
    traceId: typeof problem["traceId"] === "string" ? problem["traceId"] : undefined,
    requestId: facts.requestId,
    fieldErrors: readFieldErrors(problem["errors"]),
    retryAfter: facts.retryAfter,
  });
}

/**
 * RFC 6749 §5.2 bodies: `error` is a lowercase underscore token, so
 * `invalid_grant` becomes `OAuth.InvalidGrant`. The token is the title, the
 * description the detail. The grammar is documented, not enumerated: an
 * extension token maps the same way.
 */
function oauthFailure(
  body: Record<string, unknown>,
  token: string,
  facts: ResponseFacts,
): ApiFailure {
  const description: unknown = body["error_description"];

  return new ApiFailure({
    status: facts.status ?? readBodyStatus(body) ?? INTERNAL_SERVER_ERROR,
    code: `OAuth.${toPascalCase(token)}`,
    title: token,
    detail: typeof description === "string" ? description : undefined,
    requestId: facts.requestId,
    retryAfter: facts.retryAfter,
  });
}

function unrecognized(cause: unknown, facts: ResponseFacts): ApiFailure {
  return new ApiFailure({
    status: facts.status ?? INTERNAL_SERVER_ERROR,
    code: ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE,
    title: UNRECOGNIZED_TITLE,
    requestId: facts.requestId,
    retryAfter: facts.retryAfter,
    cause,
  });
}

function transportFailure(error: Error, requestId: string | undefined): ApiFailure {
  if (isTimeout(error)) {
    return new ApiFailure({
      status: GATEWAY_TIMEOUT,
      code: ClientErrorCode.TRANSPORT_TIMEOUT,
      title: "The request timed out",
      requestId,
      cause: error,
    });
  }

  if (error.name === "AbortError") {
    return new ApiFailure({
      status: CLIENT_CLOSED_REQUEST,
      code: ClientErrorCode.TRANSPORT_ABORTED,
      title: "The request was aborted",
      requestId,
      cause: error,
    });
  }

  return new ApiFailure({
    status: SERVICE_UNAVAILABLE,
    code: ClientErrorCode.TRANSPORT_NETWORK_ERROR,
    title: "Unable to reach the server",
    requestId,
    cause: error,
  });
}

/**
 * A `TimeoutError` (what `AbortSignal.timeout()` aborts with), or an undici
 * timeout code on the error itself or on the error fetch wrapped it in.
 */
function isTimeout(error: Error): boolean {
  const seen = new Set<Error>();
  let current: Error | undefined = error;

  while (current !== undefined && !seen.has(current) && seen.size < MAX_CAUSE_DEPTH) {
    if (current.name === "TimeoutError" || hasTimeoutCode(current)) {
      return true;
    }

    seen.add(current);
    current = current.cause instanceof Error ? current.cause : undefined;
  }

  return false;
}

function hasTimeoutCode(error: Error): boolean {
  const code: unknown = (error as { code?: unknown }).code;

  return typeof code === "string" && TIMEOUT_CODES.has(code);
}

/**
 * `Retry-After` is either delta-seconds or an HTTP-date (RFC 9110 §10.2.3); a
 * date becomes whole seconds from now, clamped at zero. Anything else is
 * ignored rather than guessed at. Exported for a caller that has the header but
 * hands {@link toApiFailure} a body it already parsed.
 */
export function parseRetryAfter(header: string | null): number | undefined {
  if (header === null || header.trim() === "") {
    return undefined;
  }

  const value: string = header.trim();
  if (DELTA_SECONDS.test(value)) {
    return Number(value);
  }

  if (!HTTP_DATE.test(value)) {
    return undefined;
  }

  const at: number = Date.parse(value);
  if (Number.isNaN(at)) {
    return undefined;
  }

  return Math.max(NO_WAIT, Math.ceil((at - Date.now()) / MILLISECONDS_PER_SECOND));
}

/**
 * Validation messages keyed by property, as ASP.NET Core emits them. The body
 * is untrusted wire data, so an entry survives only when it is an array of
 * strings, and a body that leaves none standing yields `undefined` rather than
 * an empty record.
 */
function readFieldErrors(errors: unknown): Record<string, readonly string[]> | undefined {
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

function toPascalCase(token: string): string {
  return token
    .split(OAUTH_TOKEN_SEPARATOR)
    .filter((word: string) => word !== "")
    .map(
      (word: string) =>
        word.charAt(FIRST_CHARACTER).toUpperCase() + word.slice(REST_OF_WORD).toLowerCase(),
    )
    .join("");
}

function tryParseJson(text: string): unknown {
  if (text.trim() === "") {
    return undefined;
  }

  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isStringArray(value: unknown): value is readonly string[] {
  return Array.isArray(value) && value.every((item: unknown) => typeof item === "string");
}
