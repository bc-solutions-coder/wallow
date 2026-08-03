/**
 * The wire contract both entries speak, plus the two pure passes that operate on
 * it: redaction and batch validation.
 *
 * This module is imported by `./index` (the browser core, which produces a
 * batch) and by `./server` (the ingest handler, which consumes one), and both
 * entries re-export the types. Sender and receiver therefore drift into a type
 * error rather than into a silently discarded field.
 */

/** Severity, lowest to highest. The order of {@link LOG_LEVELS} is the ordering. */
export type LogLevel = "debug" | "info" | "warn" | "error";

/** Every level, ascending. An index into this array IS the level's severity. */
export const LOG_LEVELS: readonly LogLevel[] = ["debug", "info", "warn", "error"];

/** Whether `level` is at or above `minimum` — the level filter, in one place. */
export function isAtLeast(level: LogLevel, minimum: LogLevel): boolean {
  return LOG_LEVELS.indexOf(level) >= LOG_LEVELS.indexOf(minimum);
}

/** An error carried on a record: the three fields that survive JSON. */
export interface LogEventError {
  name: string;
  message: string;
  stack?: string;
}

/**
 * One log record as it travels from the browser to the app server.
 *
 * `event` is a NAME, not prose — dotted, low-cardinality, groupable
 * (`form.submitted`, `bff.logout.failed`). Free-text messages are unqueryable at
 * the volume this package exists to handle; whatever varies per occurrence goes
 * in `attrs`.
 */
export interface LogEvent {
  /** When the browser recorded it, ISO 8601. The server keeps its own receipt time. */
  ts: string;
  level: LogLevel;
  event: string;
  attrs: Record<string, unknown>;
  correlationId?: string;
  error?: LogEventError;
}

/**
 * A flush: the events, plus — on the `sendBeacon` path only — the CSRF token
 * that could not ride on a header.
 *
 * `sendBeacon` cannot set headers, so an app whose ingest route is CSRF-gated
 * has exactly two choices: put the token in the body or lose every log a closing
 * tab was holding. The ingest handler accepts it from either place.
 */
export interface LogBatch {
  events: LogEvent[];
  csrfToken?: string;
}

/** The value substituted for a redacted attribute. */
export const REDACTED: string = "[redacted]";

/**
 * Attribute keys scrubbed unless a consumer names its own list.
 *
 * Matched case-insensitively as a SUBSTRING, so `password` also covers
 * `newPassword` and `password_confirmation`. A short list of things that are
 * never safe to ship offsite, not an attempt at classification.
 */
export const DEFAULT_REDACT_KEYS: readonly string[] = [
  "password",
  "token",
  "secret",
  "authorization",
  "cookie",
  "email",
];

/** How deep {@link redactAttrs} walks a nested attribute value. */
const MAX_REDACT_DEPTH = 4;

/** The depth a top-level attribute bag sits at. */
const TOP_LEVEL = 0;

/** One step further into a nested bag. */
const ONE_LEVEL = 1;

/** An empty batch. */
const NONE = 0;

/** Whether `key` matches any redaction key, case-insensitively, as a substring. */
function isRedactedKey(key: string, keys: readonly string[]): boolean {
  const lower: string = key.toLowerCase();
  return keys.some((candidate: string): boolean => lower.includes(candidate.toLowerCase()));
}

/** Whether `value` is a plain object worth walking into. */
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Key-based scrubbing over an attribute bag.
 *
 * Runs TWICE by design: client-side before an event leaves the browser, so PII
 * never sits in a buffer or a beacon body, and again server-side, which is the
 * authoritative pass because a fork cannot bypass it from a page it does not
 * control.
 *
 * Depth-limited rather than fully recursive: an attribute bag deep enough to
 * exhaust {@link MAX_REDACT_DEPTH} is a payload, not a log record, and a cyclic
 * one would otherwise hang the request that carries it.
 */
export function redactAttrs(
  attrs: Record<string, unknown>,
  keys: readonly string[] = DEFAULT_REDACT_KEYS,
  depth: number = TOP_LEVEL,
): Record<string, unknown> {
  const result: Record<string, unknown> = {};

  for (const [key, value] of Object.entries(attrs)) {
    if (isRedactedKey(key, keys)) {
      result[key] = REDACTED;
    } else if (isRecord(value) && depth < MAX_REDACT_DEPTH) {
      result[key] = redactAttrs(value, keys, depth + ONE_LEVEL);
    } else {
      result[key] = value;
    }
  }

  return result;
}

/**
 * The caps the ingest handler enforces and the browser core respects.
 *
 * `maxBodyBytes` is 64 KiB because that is `sendBeacon`'s own quota — the
 * ceiling, not the budget. A batch that would exceed it is rejected rather than
 * truncated: a half-parsed record is a record whose meaning changed in transit.
 */
export interface IngestLimits {
  maxBodyBytes: number;
  maxEventsPerBatch: number;
  maxEventNameLength: number;
  maxAttributesPerEvent: number;
}

/** Bytes in 64 KiB — `sendBeacon`'s per-origin quota. */
const SIXTY_FOUR_KIB = 65_536;

/** The default caps. Every one of them is overridable per app. */
export const DEFAULT_INGEST_LIMITS: IngestLimits = {
  maxBodyBytes: SIXTY_FOUR_KIB,
  maxEventsPerBatch: 100,
  maxEventNameLength: 120,
  maxAttributesPerEvent: 32,
};

/**
 * The shape an event name must have: dotted, lowercase-ish segments.
 *
 * Enforced on the wire, not merely documented, because the cardinality of this
 * field is what decides whether a Grafana query over a week of logs returns in a
 * second or not at all.
 */
const EVENT_NAME_PATTERN: RegExp = /^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$/u;

/** Whether `value` is a usable event name at the given cap. */
export function isValidEventName(value: string, maxLength: number): boolean {
  return value.length <= maxLength && EVENT_NAME_PATTERN.test(value);
}

/** A rejected batch, with the reason a caller may safely be told. */
interface InvalidBatch {
  ok: false;
  reason: string;
}

/** An accepted batch. */
interface ValidBatch {
  ok: true;
  batch: LogBatch;
}

/** The result of {@link parseLogBatch}. */
export type BatchResult = ValidBatch | InvalidBatch;

/** Whether `value` is an ISO-8601 instant a `Date` round-trips. */
function isIsoTimestamp(value: unknown): value is string {
  return typeof value === "string" && !Number.isNaN(Date.parse(value));
}

/** Whether `value` is one of the four levels. */
function isLogLevel(value: unknown): value is LogLevel {
  return typeof value === "string" && (LOG_LEVELS as readonly string[]).includes(value);
}

/** Validate one event, answering the reason it fails. */
function eventReason(value: unknown, limits: IngestLimits): string | undefined {
  if (!isRecord(value)) {
    return "event is not an object";
  }
  if (!isLogLevel(value["level"])) {
    return "event has no valid level";
  }
  if (
    typeof value["event"] !== "string" ||
    !isValidEventName(value["event"], limits.maxEventNameLength)
  ) {
    return "event name is not a dotted low-cardinality name";
  }
  if (!isIsoTimestamp(value["ts"])) {
    return "event has no valid ts";
  }

  const attrs: unknown = value["attrs"];
  if (attrs !== undefined && !isRecord(attrs)) {
    return "event attrs is not an object";
  }
  if (isRecord(attrs) && Object.keys(attrs).length > limits.maxAttributesPerEvent) {
    return "event carries too many attributes";
  }

  return undefined;
}

/**
 * Validate a decoded request body as a {@link LogBatch}.
 *
 * Total: it never throws, and every rejection names a reason that describes the
 * SHAPE of the payload rather than anything the sender supplied — the reason
 * travels back to an unauthenticated caller.
 */
export function parseLogBatch(
  value: unknown,
  limits: IngestLimits = DEFAULT_INGEST_LIMITS,
): BatchResult {
  if (!isRecord(value)) {
    return { ok: false, reason: "body is not an object" };
  }

  const events: unknown = value["events"];
  if (!Array.isArray(events)) {
    return { ok: false, reason: "body has no events array" };
  }
  if (events.length === NONE) {
    return { ok: false, reason: "batch is empty" };
  }
  if (events.length > limits.maxEventsPerBatch) {
    return { ok: false, reason: "batch holds too many events" };
  }

  for (const event of events) {
    const reason: string | undefined = eventReason(event, limits);
    if (reason !== undefined) {
      return { ok: false, reason };
    }
  }

  const csrfToken: unknown = value["csrfToken"];
  if (csrfToken !== undefined && typeof csrfToken !== "string") {
    return { ok: false, reason: "csrfToken is not a string" };
  }

  // Rebuilt field by field rather than spread: the loop above proved these five
  // are well-formed, and copying only them means a payload carrying an extra key
  // — a `service`, a `userId` — cannot smuggle it past validation into a record.
  const normalized: LogEvent[] = [];
  for (const event of events as LogEvent[]) {
    normalized.push({
      ts: event.ts,
      level: event.level,
      event: event.event,
      attrs: event.attrs ?? {},
      ...(event.correlationId === undefined ? {} : { correlationId: event.correlationId }),
      ...(event.error === undefined ? {} : { error: event.error }),
    });
  }

  return {
    ok: true,
    batch: {
      events: normalized,
      ...(typeof csrfToken === "string" ? { csrfToken } : {}),
    },
  };
}

/**
 * The correlation header this package reads, mirroring the SDK's
 * `REQUEST_ID_HEADER`.
 *
 * Declared here rather than imported: a logger that depended on
 * `@bc-solutions-coder/sdk` for one string would drag a published package with
 * an OIDC client into every consumer's graph. The two constants are pinned to
 * each other by an app-side spec, in the one place that already depends on both.
 *
 * It is the only header this package reads a value out of. The client address
 * deliberately is not one: see `clientAddress` on `LogIngestOptions`.
 */
export const REQUEST_ID_HEADER: string = "x-request-id";
