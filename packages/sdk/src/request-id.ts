/**
 * The `x-request-id` correlation contract, shared by both entry points.
 *
 * Every request through the BFF tunnel carries an `x-request-id`: the caller's
 * when it supplied one, a freshly generated one when it did not. The id goes
 * upstream to the API, comes back on the BFF's own response, and rides on every
 * `ApiFailure` the tunnel raises — so an error a user reports in the
 * browser names the exact request that produced it. Pairing that id with the
 * `traceId` the API's problem details already carry is what turns a frontend
 * error into a backend OTel trace; the workflow is written up in
 * `docs/operations/request-correlation.md`.
 *
 * This module lives at the package root rather than under `server/` because
 * the browser reads the header off a response and the BFF writes it onto a
 * request, and the two must agree on the name and on what counts as a usable
 * id. It is dependency-free and runs in either runtime.
 */

/** The correlation header carried on every request through the BFF tunnel. */
export const REQUEST_ID_HEADER: string = "x-request-id";

/**
 * Longest inbound request id echoed rather than replaced.
 *
 * An id is copied into an outbound header, a log line, and a trace tag, so an
 * unbounded caller-supplied value is an amplification primitive. Well past any
 * real id (a UUID is 36 characters, a W3C `traceparent` 55) and far short of
 * anything a header budget would notice.
 */
export const MAX_REQUEST_ID_LENGTH = 200;

/**
 * The characters an echoed id may consist of.
 *
 * Wide enough for every id shape that actually reaches a BFF — a UUID, a W3C
 * `traceparent`, a bare hex span id, an opaque gateway id such as
 * `req_01HQ8Z.4K9` — and nothing else. Anchored whole-string, so a single
 * disallowed character rejects the id rather than being stripped out of it:
 * a partially sanitized correlation key no longer matches what the caller
 * logged, which defeats the point of echoing it.
 */
const REQUEST_ID_CHARSET: RegExp = /^[A-Za-z0-9._:-]+$/u;

/**
 * Whether `value` is safe to echo back onto a header, a log line, and a trace
 * tag.
 *
 * The charset is deliberately narrow rather than "whatever a header field-value
 * allows": the id is a correlation key, not a message. Whitespace, control
 * characters, and CR/LF in particular never appear in a real id and are exactly
 * what a caller would use to forge a second header or a second log record.
 */
export function isValidRequestId(value: string): boolean {
  // The charset's `+` is what rejects the empty string: an id of no characters
  // correlates nothing, so it is replaced rather than echoed.
  return value.length <= MAX_REQUEST_ID_LENGTH && REQUEST_ID_CHARSET.test(value);
}

/**
 * A fresh request id, unique per call.
 */
export function newRequestId(): string {
  return crypto.randomUUID();
}

/**
 * The request id for an inbound request: the caller's when `headers` carries a
 * usable {@link REQUEST_ID_HEADER}, a fresh one otherwise.
 *
 * Always answers an id — a request with no usable correlation key gets one
 * rather than travelling uncorrelated.
 */
export function resolveRequestId(headers: Headers): string {
  const inbound: string | null = headers.get(REQUEST_ID_HEADER);

  return inbound !== null && isValidRequestId(inbound) ? inbound : newRequestId();
}
