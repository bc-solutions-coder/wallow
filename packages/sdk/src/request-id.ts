/** Request correlation helpers shared by browser and server callers. */

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
 * Check whether a correlation ID can be forwarded in `x-request-id`.
 *
 * Accepts 1–200 ASCII letters, digits, periods, underscores, colons, or hyphens.
 * Empty values, whitespace, and other characters are rejected.
 */
export function isValidRequestId(value: string): boolean {
  // The charset's `+` is what rejects the empty string: an id of no characters
  // correlates nothing, so it is replaced rather than echoed.
  return value.length <= MAX_REQUEST_ID_LENGTH && REQUEST_ID_CHARSET.test(value);
}

/**
 * Generate a UUID correlation ID using `crypto.randomUUID()`.
 */
export function newRequestId(): string {
  return crypto.randomUUID();
}

/**
 * Read a valid `x-request-id` header, or generate a UUID when it is missing or invalid.
 */
export function resolveRequestId(headers: Headers): string {
  const inbound: string | null = headers.get(REQUEST_ID_HEADER);

  return inbound !== null && isValidRequestId(inbound) ? inbound : newRequestId();
}
