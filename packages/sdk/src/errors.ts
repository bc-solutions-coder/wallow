/**
 * The error type shared by both entry points.
 *
 * {@link WallowError} lives here rather than under `server/` so the browser
 * entry and the server entry resolve the *same* class — a consumer catching an
 * error from the BFF tunnel and a consumer catching one in the browser are
 * looking at one type, not two structurally identical ones.
 *
 * {@link isWallowError} is the supported way to recognize one. It is a brand
 * check, never `instanceof`: bundlers and test runners routinely load a module
 * twice (dual ESM/CJS resolution, a duplicated dependency in the graph, a
 * re-evaluated module registry), which yields two distinct class objects and
 * breaks `instanceof` for errors that crossed the boundary.
 */

/**
 * The brand marker, taken from the *global* symbol registry so two copies of
 * this module produce the same key. It is set as an own property in the
 * constructor, which keeps it reachable even when the prototype chain has been
 * replaced, and it cannot be reproduced by a structurally similar plain object.
 */
const WALLOW_ERROR_BRAND: unique symbol = Symbol.for("wallow.error");

/**
 * An error raised by the BFF for a failed upstream call.
 *
 * `status` is the HTTP status of the upstream response, `code` is the
 * machine-readable error code, and `title`/`detail` mirror the RFC 7807 members.
 *
 * `status` is always a number, so a request that never produced a response has
 * to be recognisable by its `code` rather than by an absent status: a fault that
 * never landed carries `503` with code `NETWORK_ERROR` (`NETWORK_ERROR_CODE`,
 * exported from the `./server` entry), whether it was the browser's call to the
 * BFF or the BFF's forward to the API that dropped.
 *
 * `requestId` and `traceId` are the two halves of the correlation story
 * (Wallow-pu6a.6.7): the first is the `x-request-id` the BFF stamped on the
 * request and echoed on the response, the second is the W3C trace id the API
 * puts in its problem details and OTel exports to Tempo. Either one takes a
 * reported error to the backend work that produced it — see
 * `docs/operations/request-correlation.md`.
 *
 * `fieldErrors` is the RFC 7807 `errors` member a validation failure carries. It
 * is the only part of the payload that says WHICH property a message belongs to,
 * so without it a form catching a 400 can do nothing better than repeat `title`
 * in a banner.
 */
export class WallowError extends Error {
  readonly [WALLOW_ERROR_BRAND]: true = true;
  readonly status: number;
  readonly code: string;
  readonly title: string;
  readonly detail?: string;
  /** The `x-request-id` this request carried through the BFF tunnel. */
  readonly requestId?: string;
  /** The backend's W3C trace id, as the API's problem details report it. */
  readonly traceId?: string;
  /**
   * The RFC 7807 `errors` member — validation messages keyed by property name,
   * as ASP.NET Core's `ValidationProblemDetails` emits them, when the API sent
   * any.
   */
  readonly fieldErrors?: Readonly<Record<string, readonly string[]>>;

  constructor(init: {
    status: number;
    code: string;
    title: string;
    detail?: string;
    requestId?: string;
    traceId?: string;
    fieldErrors?: Readonly<Record<string, readonly string[]>>;
  }) {
    super(init.detail ? `${init.title}: ${init.detail}` : init.title);
    this.name = "WallowError";
    this.status = init.status;
    this.code = init.code;
    this.title = init.title;
    this.detail = init.detail;
    this.requestId = init.requestId;
    this.traceId = init.traceId;
    this.fieldErrors = init.fieldErrors;
  }
}

/**
 * Narrows `value` to a {@link WallowError} via a brand marker.
 *
 * Deliberately not `instanceof` — see the module doc comment — and deliberately
 * not a duck-type check on `status`/`code`/`title`, which any plain object could
 * satisfy by accident.
 */
export function isWallowError(value: unknown): value is WallowError {
  return (
    typeof value === "object" &&
    value !== null &&
    (value as { [WALLOW_ERROR_BRAND]?: unknown })[WALLOW_ERROR_BRAND] === true
  );
}
