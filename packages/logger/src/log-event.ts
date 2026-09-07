/**
 * Shared browser-to-server event contract, attribute redaction, and validation of decoded log
 * batches.
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
  /**
   * Error name recorded with the event.
   */
  name: string;
  /**
   * Error text, not covered by attribute-key redaction.
   */
  message: string;
  /**
   * Optional stack trace, not covered by attribute-key redaction.
   */
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
  /**
   * Event severity used by filtering and OTLP encoding.
   */
  level: LogLevel;
  /**
   * Stable event identifier, such as form.submitted.
   */
  event: string;
  /**
   * Application attributes; keep values serializable and free of sensitive content.
   */
  attrs: Record<string, unknown>;
  /**
   * Caller-supplied correlation identifier; the server preserves it when present.
   */
  correlationId?: string;
  /**
   * Optional diagnostic error fields.
   */
  error?: LogEventError;
}

/**
 * Events sent in one request, with an optional CSRF token for beacon delivery. The app authorize
 * callback decides how to validate the token.
 */
export interface LogBatch {
  /**
   * Nonempty event batch subject to the ingest event-count limit.
   */
  events: LogEvent[];
  /**
   * Optional token for the ingest authorize callback, used by beacon delivery.
   */
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
 * Copy an attribute object and replace keys matching a configured substring, ignoring case.
 * Descends into object values through depth four; arrays and deeper object values are retained
 * unchanged. Does not scrub scalar content or error fields, so callers must avoid placing secrets
 * there.
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
 * Server-side batch limits. The browser logger has separate buffer and serialized-body options; it
 * does not automatically inherit these caps.
 */
export interface IngestLimits {
  /**
   * Maximum UTF-8 request body size enforced by ingestion.
   */
  maxBodyBytes: number;
  /**
   * Maximum records accepted in one decoded batch.
   */
  maxEventsPerBatch: number;
  /**
   * Maximum event-name length in JavaScript string code units.
   */
  maxEventNameLength: number;
  /**
   * Maximum number of top-level attribute keys per event.
   */
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
 * Allowed event-name syntax: lowercase alphanumeric segments with dot, underscore, or hyphen separators.
 */
const EVENT_NAME_PATTERN: RegExp = /^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$/u;

/**
 * Check a lowercase event name against the length cap. Names begin with a letter and contain
 * lowercase letters or digits separated by single dots, underscores, or hyphens. This checks
 * syntax, not cardinality.
 */
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

/**
 * Whether value is a string accepted by Date.parse. This does not enforce ISO syntax.
 */
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
 * Validate an already decoded JSON batch and rebuild its event records. Checks nonempty batch
 * size, level, event name, parseable timestamp, attribute object size, and optional CSRF token
 * type. Unknown event fields are omitted; correlationId and error are carried through without
 * further validation. Body byte limits are enforced by the ingest handler, not this function.
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

  // Copy the known event fields and omit extra top-level fields.
  // The correlationId and error values are retained without additional validation.
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
 * Correlation header used as an ingest fallback when an event has no correlationId. Declared
 * locally so the logger does not depend on the SDK.
 */
export const REQUEST_ID_HEADER: string = "x-request-id";
