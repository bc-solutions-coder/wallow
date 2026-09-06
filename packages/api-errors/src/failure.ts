/**
 * The one failure type every Wallow API consumer handles.
 *
 * A problem the API answered with, a transport fault the client hit on the way,
 * and a response the client could not make sense of all arrive as an
 * {@link ApiFailure}, so a caller branches on {@link ApiFailure.code} and
 * {@link ApiFailure.status} and never on the wire format.
 */

/**
 * The brand {@link isApiFailure} checks for. A global-registry symbol rather
 * than an `instanceof`: an app and a library it consumes may each bundle their
 * own copy of this module, and the two classes must still recognise each
 * other's failures.
 */
const API_FAILURE_BRAND: unique symbol = Symbol.for(
  "wallow.api-failure",
) as typeof API_FAILURE_BRAND;

/** What a failure is built from. Every member but the first three is optional. */
export interface ApiFailureInit {
  /** The HTTP status the failure is answered with. */
  readonly status: number;
  /** The machine-readable code: a catalogue `ErrorCode` or a `ClientErrorCode`. */
  readonly code: string;
  /** The problem's short summary, for logs and as the last-resort caption. */
  readonly title: string;
  /** The problem's human-readable explanation, when the API sent one. */
  readonly detail?: string | undefined;
  /** The W3C trace id the API stamped on the problem. */
  readonly traceId?: string | undefined;
  /** The request id from the response's `x-request-id` header (the BFF tunnel's). */
  readonly requestId?: string | undefined;
  /** Validation messages keyed by property name, on a 400. */
  readonly fieldErrors?: Readonly<Record<string, readonly string[]>> | undefined;
  /** Seconds to wait before retrying, from a `Retry-After` header. */
  readonly retryAfter?: number | undefined;
  /**
   * What the failure was built from when that was not a problem: the thrown
   * error, or the body text of an unrecognised response. Never rendered.
   */
  readonly cause?: unknown;
}

/**
 * A failed API call. `message` reads `[<status> <code>] <title>` and is meant
 * for a log line; a sentence for a person comes from `resolveFailureMessage`.
 */
// oxlint-disable-next-line unicorn/custom-error-definition -- the name is the contract's; every consumer says "failure", not "error"
export class ApiFailure extends Error {
  readonly [API_FAILURE_BRAND]: true = true;
  readonly status: number;
  readonly code: string;
  readonly title: string;
  readonly detail: string | undefined;
  readonly traceId: string | undefined;
  readonly requestId: string | undefined;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>> | undefined;
  readonly retryAfter: number | undefined;

  constructor(init: ApiFailureInit) {
    super(
      `[${init.status} ${init.code}] ${init.title}`,
      init.cause === undefined ? undefined : { cause: init.cause },
    );
    this.name = "ApiFailure";
    this.status = init.status;
    this.code = init.code;
    this.title = init.title;
    this.detail = init.detail;
    this.traceId = init.traceId;
    this.requestId = init.requestId;
    this.fieldErrors = init.fieldErrors;
    this.retryAfter = init.retryAfter;
  }
}

/**
 * Whether `value` is an {@link ApiFailure} — from this copy of the module or
 * from any other copy that shares the process.
 */
export function isApiFailure(value: unknown): value is ApiFailure {
  return (
    typeof value === "object" &&
    value !== null &&
    (value as { [API_FAILURE_BRAND]?: unknown })[API_FAILURE_BRAND] === true
  );
}
