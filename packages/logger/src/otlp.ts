/**
 * Encode server records as OTLP HTTP JSON and send them to a collector.
 */

import type { LogEvent, LogLevel } from "./log-event";

/**
 * A record as the SERVER holds it: the browser's event plus everything only the
 * server may assert.
 *
 * The split matters. `ts` is the server's receipt time and `clientTs` is what the
 * page claimed, because a clock the user controls cannot be allowed to order a
 * shared log stream. `service`, `clientIp`, `userId` and `tenantId` are stamped
 * here and never read off the wire — a record that names its own service or user
 * is one any page can forge.
 */
export interface ServerLogRecord {
  /** Server receipt time, ISO 8601. */
  ts: string;
  /** What the browser claimed, preserved for clock-skew analysis. */
  clientTs: string;
  /**
   * Severity encoded in the OTLP record.
   */
  level: LogLevel;
  /**
   * Stable event name used as the OTLP body and event.name attribute.
   */
  event: string;
  /**
   * Application attributes; dedicated server fields win on reserved-name collisions.
   */
  attrs: Record<string, unknown>;
  /** Stamped by the handler from its own configuration. */
  service: string;
  /**
   * Correlation identifier from the event or request-header fallback.
   */
  correlationId?: string;
  /**
   * Client address resolved by the trusted host callback.
   */
  clientIp?: string;
  /**
   * User identifier supplied by server context.
   */
  userId?: string;
  /**
   * Tenant identifier supplied by server context.
   */
  tenantId?: string;
  /**
   * Diagnostic error fields mapped to OTLP exception attributes.
   */
  error?: LogEvent["error"];
}

/**
 * OTLP severity numbers for debug, info, warn, and error.
 */
const SEVERITY_NUMBERS: Record<LogLevel, number> = {
  debug: 5,
  info: 9,
  warn: 13,
  error: 17,
};

/** Nanoseconds per millisecond — OTLP timestamps are `timeUnixNano`. */
const NANOS_PER_MILLI = 1_000_000n;

/** An empty batch. */
const NONE = 0;

/** An OTLP `AnyValue`, restricted to what a log attribute can carry. */
interface OtlpAnyValue {
  stringValue?: string;
  boolValue?: boolean;
  intValue?: string;
  doubleValue?: number;
}

/** An OTLP key/value attribute. */
interface OtlpAttribute {
  key: string;
  value: OtlpAnyValue;
}

/** One OTLP log record. */
interface OtlpLogRecord {
  timeUnixNano: string;
  observedTimeUnixNano: string;
  severityNumber: number;
  severityText: string;
  body: OtlpAnyValue;
  attributes: OtlpAttribute[];
}

/** The `POST /v1/logs` body. */
export interface OtlpLogsPayload {
  resourceLogs: {
    resource: { attributes: OtlpAttribute[] };
    scopeLogs: { scope: { name: string }; logRecords: OtlpLogRecord[] }[];
  }[];
}

/** The instrumentation scope every record from this package is attributed to. */
const SCOPE_NAME: string = "@bc-solutions-coder/logger";

/**
 * Encode one JS value as an OTLP `AnyValue`.
 *
 * Anything that is not a primitive is JSON-stringified into `stringValue` rather
 * than dropped: OTLP's `kvlistValue`/`arrayValue` survive the wire but most
 * backends flatten them anyway, and a nested attribute that silently vanishes is
 * worse than one that arrives as text.
 */
function toAnyValue(value: unknown): OtlpAnyValue {
  if (typeof value === "string") {
    return { stringValue: value };
  }
  if (typeof value === "boolean") {
    return { boolValue: value };
  }
  if (typeof value === "number") {
    return Number.isInteger(value) ? { intValue: String(value) } : { doubleValue: value };
  }
  if (value === undefined || value === null) {
    return { stringValue: "" };
  }

  try {
    return { stringValue: JSON.stringify(value) ?? "" };
  } catch {
    // A cyclic or unserialisable attribute must not cost the whole batch.
    return { stringValue: "[unserializable]" };
  }
}

/** Build the attribute list, skipping keys whose value was never set. */
function toAttributes(entries: Record<string, unknown>): OtlpAttribute[] {
  return Object.entries(entries)
    .filter(([, value]: [string, unknown]): boolean => value !== undefined)
    .map(([key, value]: [string, unknown]): OtlpAttribute => ({ key, value: toAnyValue(value) }));
}

/** ISO 8601 → OTLP `timeUnixNano`, as the decimal string OTLP/JSON expects. */
function toUnixNano(iso: string, fallbackMs: number): string {
  const millis: number = Date.parse(iso);
  const usable: number = Number.isNaN(millis) ? fallbackMs : millis;
  return String(BigInt(usable) * NANOS_PER_MILLI);
}

/**
 * Group records by service and encode an OTLP JSON logs payload. Uses clientTs for event time and
 * ts for observed time, falling back to nowMs for unparseable dates. Object attributes become JSON
 * strings; known server fields overwrite colliding attribute names. nowMs must be integer epoch
 * milliseconds.
 */
export function toOtlpLogsPayload(records: ServerLogRecord[], nowMs: number): OtlpLogsPayload {
  const byService = new Map<string, ServerLogRecord[]>();

  for (const record of records) {
    const bucket: ServerLogRecord[] | undefined = byService.get(record.service);
    if (bucket === undefined) {
      byService.set(record.service, [record]);
    } else {
      bucket.push(record);
    }
  }

  return {
    resourceLogs: [...byService].map(([service, serviceRecords]: [string, ServerLogRecord[]]) => ({
      resource: { attributes: toAttributes({ "service.name": service }) },
      scopeLogs: [
        {
          scope: { name: SCOPE_NAME },
          logRecords: serviceRecords.map(
            (record: ServerLogRecord): OtlpLogRecord => ({
              timeUnixNano: toUnixNano(record.clientTs, nowMs),
              observedTimeUnixNano: toUnixNano(record.ts, nowMs),
              severityNumber: SEVERITY_NUMBERS[record.level],
              severityText: record.level.toUpperCase(),
              body: { stringValue: record.event },
              attributes: toAttributes({
                ...record.attrs,
                "event.name": record.event,
                "wallow.correlation_id": record.correlationId,
                "client.address": record.clientIp,
                "enduser.id": record.userId,
                "wallow.tenant_id": record.tenantId,
                "exception.type": record.error?.name,
                "exception.message": record.error?.message,
                "exception.stacktrace": record.error?.stack,
              }),
            }),
          ),
        },
      ],
    })),
  };
}

/**
 * The logs URL for a collector base endpoint.
 *
 * `OTEL_EXPORTER_OTLP_ENDPOINT` is the base (`http://localhost:4318`) and the
 * signal path is appended; `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` is already the full
 * URL. Callers that hand in a value already ending in `/v1/logs` get it back
 * unchanged, so either variable can be passed without the caller having to know
 * which convention it followed.
 */
export function otlpLogsUrl(endpoint: string): string {
  const trimmed: string = endpoint.replace(/\/+$/u, "");
  return trimmed.endsWith("/v1/logs") ? trimmed : `${trimmed}/v1/logs`;
}

/** How {@link emitOtlp} reports what happened, without throwing at its caller. */
export interface OtlpEmitResult {
  /**
   * Whether no send was needed or the collector returned a successful HTTP status.
   */
  ok: boolean;
  /**
   * Collector HTTP status when a response was received.
   */
  status?: number;
  /**
   * Caught serialization or transport failure when no response was produced.
   */
  error?: unknown;
}

/**
 * POST records as OTLP JSON to the collector logs URL. Empty input succeeds without a network
 * request. Returns the HTTP status for a response or the caught error for encoding and transport
 * failures; does not throw those failures or retry them.
 *
 * @param endpoint Collector base URL or complete /v1/logs URL.
 *
 * @param records Server log records to encode.
 *
 * @param nowMs Integer epoch milliseconds used for invalid timestamp fallback.
 *
 * @param fetchImpl Transport override, defaulting to fetch.
 */
export async function emitOtlp(
  endpoint: string,
  records: ServerLogRecord[],
  nowMs: number,
  fetchImpl: typeof fetch = fetch,
): Promise<OtlpEmitResult> {
  if (records.length === NONE) {
    return { ok: true };
  }

  try {
    const response: Response = await fetchImpl(otlpLogsUrl(endpoint), {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(toOtlpLogsPayload(records, nowMs)),
    });
    return { ok: response.ok, status: response.status };
  } catch (error: unknown) {
    return { ok: false, error };
  }
}
