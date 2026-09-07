import { LIMITS } from "./limits.js";
import { sanitizeAttributes, sanitizeUrl } from "./privacy.js";

export const BROWSER_LIMITS = {
  batchBytes: 49_152,
  bufferedBytes: 262_144,
  eventBytes: 8192,
  events: 256,
  attributes: 32,
  flushMs: 5000,
  concurrent: 16,
  sessionSeconds: 3600,
};
const START = 0;
const ONE = 1;
const encoder = new TextEncoder();
export type FaroRecord = Record<string, unknown>;
export interface FaroBatch {
  logs: FaroRecord[];
  exceptions: FaroRecord[];
  measurements: FaroRecord[];
  events: FaroRecord[];
  traces: FaroRecord;
}
export function record(value: unknown): FaroRecord {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? Object.fromEntries(Object.entries(value))
    : {};
}
function list(value: unknown): unknown[] {
  return Array.isArray(value) ? value.slice(START, BROWSER_LIMITS.events) : [];
}
function name(value: unknown, fallback: string): string {
  return typeof value === "string" && /^[a-zA-Z][a-zA-Z0-9_.-]{0,127}$/u.test(value)
    ? value
    : fallback;
}
function timestamp(value: unknown): string {
  return typeof value === "string" &&
    value.length <= BROWSER_LIMITS.attributes &&
    Number.isFinite(Date.parse(value))
    ? new Date(value).toISOString()
    : new Date().toISOString();
}
function context(value: unknown): Record<string, string> {
  return Object.fromEntries(
    Object.entries(sanitizeAttributes(value)).map(([key, item]) => {
      const text = typeof item === "string" ? item : JSON.stringify(item);
      return [key, encoder.encode(text).byteLength <= LIMITS.valueBytes ? text : "[oversize]"];
    }),
  );
}

function traceContext(value: unknown): FaroRecord {
  const source = record(value);
  return typeof source.trace_id === "string" &&
    /^[a-f0-9]{32}$/u.test(source.trace_id) &&
    typeof source.span_id === "string" &&
    /^[a-f0-9]{16}$/u.test(source.span_id)
    ? { trace_id: source.trace_id, span_id: source.span_id }
    : {};
}
function bounded(
  items: unknown,
  onDrop: ((count: number) => void) | undefined,
  transform: (value: FaroRecord) => FaroRecord,
): FaroRecord[] {
  const source = Array.isArray(items) ? items : [];
  if (source.length > BROWSER_LIMITS.events) {
    onDrop?.(source.length - BROWSER_LIMITS.events);
  }
  return list(source)
    .map((item) => transform(record(item)))
    .filter((item) => {
      const accepted = encoder.encode(JSON.stringify(item)).byteLength <= BROWSER_LIMITS.eventBytes;
      if (!accepted) {
        onDrop?.(ONE);
      }
      return accepted;
    });
}

function frames(value: unknown): FaroRecord[] {
  return list(record(value).frames)
    .slice(START, BROWSER_LIMITS.attributes)
    .map((item) => {
      const frame = record(item);
      const filename =
        typeof frame.filename === "string"
          ? frame.filename
              .split(/[?#]/u)
              .at(START)
              ?.replaceAll("\\", "/")
              .split("/")
              .findLast(Boolean)
          : undefined;
      return {
        filename: name(filename, "unknown"),
        lineno: typeof frame.lineno === "number" ? frame.lineno : undefined,
        colno: typeof frame.colno === "number" ? frame.colno : undefined,
      };
    });
}
function otlpAttributes(value: unknown): unknown[] {
  const input = Object.fromEntries(
    list(value)
      .slice(START, BROWSER_LIMITS.attributes)
      .flatMap((item) => {
        const attribute = record(item);
        const wire = record(attribute.value);
        return typeof attribute.key === "string"
          ? [
              [
                attribute.key,
                wire.stringValue ?? wire.intValue ?? wire.doubleValue ?? wire.boolValue,
              ],
            ]
          : [];
      }),
  );
  return Object.entries(context(input)).map(([key, item]) => ({
    key,
    value: { stringValue: item },
  }));
}
function nano(value: unknown): string {
  return typeof value === "string" && /^\d{1,20}$/u.test(value) ? value : "0";
}
function traces(value: unknown, onDrop?: (count: number) => void): FaroRecord {
  return {
    resourceSpans: list(record(value).resourceSpans).map((resource) => ({
      resource: { attributes: [] },
      scopeSpans: list(record(resource).scopeSpans).map((scope) => ({
        scope: { name: "wallow.browser" },
        spans: bounded(record(scope).spans, onDrop, (span) => ({
          traceId:
            typeof span.traceId === "string" && /^[a-f0-9]{32}$/u.test(span.traceId)
              ? span.traceId
              : undefined,
          spanId:
            typeof span.spanId === "string" && /^[a-f0-9]{16}$/u.test(span.spanId)
              ? span.spanId
              : undefined,
          parentSpanId:
            typeof span.parentSpanId === "string" && /^[a-f0-9]{16}$/u.test(span.parentSpanId)
              ? span.parentSpanId
              : undefined,
          name: name(span.name, "browser.operation"),
          kind: typeof span.kind === "number" ? span.kind : undefined,
          startTimeUnixNano: nano(span.startTimeUnixNano),
          endTimeUnixNano: nano(span.endTimeUnixNano),
          attributes: otlpAttributes(span.attributes),
          events: list(span.events).map((event) => ({
            name: name(record(event).name, "browser.event"),
            timeUnixNano: nano(record(event).timeUnixNano),
            attributes: otlpAttributes(record(event).attributes),
          })),
          status: {
            code:
              typeof record(span.status).code === "number" ? record(span.status).code : undefined,
          },
        })).filter((span) => span.traceId !== undefined && span.spanId !== undefined),
      })),
    })),
  };
}

/** Rebuild every Faro signal; arbitrary metadata and fields never pass this boundary. */
export function sanitizeFaroBatch(input: unknown, onDrop?: (count: number) => void): FaroBatch {
  const source = record(input);
  return {
    logs: bounded(source.logs, onDrop, (log) => ({
      message: name(log.message, "browser.log"),
      level: name(log.level, "info"),
      timestamp: timestamp(log.timestamp),
      context: context(log.context),
      trace: traceContext(log.trace),
    })),
    exceptions: bounded(source.exceptions, onDrop, (error) => ({
      type: name(error.type, "Error"),
      value: "Unexpected application error",
      timestamp: timestamp(error.timestamp),
      stacktrace: { frames: frames(error.stacktrace) },
      context: context(error.context),
      trace: traceContext(error.trace),
    })),
    measurements: bounded(source.measurements, onDrop, (measurement) => ({
      type: name(measurement.type, "browser.performance"),
      timestamp: timestamp(measurement.timestamp),
      values: Object.fromEntries(
        Object.entries(record(measurement.values))
          .slice(START, BROWSER_LIMITS.attributes)
          .filter(
            ([key, value]) =>
              name(key, "") !== "" && typeof value === "number" && Number.isFinite(value),
          ),
      ),
      context: context(measurement.context),
    })),
    events: bounded(source.events, onDrop, (event) => ({
      name: name(event.name, "browser.event"),
      timestamp: timestamp(event.timestamp),
      attributes: context(event.attributes),
      domain: "browser",
    })),
    traces: traces(source.traces, onDrop),
  };
}

export function sanitizedPage(value: unknown): string {
  return typeof value === "string" ? sanitizeUrl(value) : "[redacted]";
}

/** Attach only context already authenticated by the application server. */
export function stampFaroContext(
  batch: FaroBatch,
  trusted: Record<string, string>,
  onDrop?: (count: number) => void,
): FaroBatch {
  const trustedKeys = new Set(Object.keys(trusted));
  const remaining = BROWSER_LIMITS.attributes - trustedKeys.size;
  const spanAttributes = Object.entries(trusted).map(([key, value]) => ({
    key,
    value: { stringValue: value },
  }));
  function stamp(items: FaroRecord[], field: string): FaroRecord[] {
    return bounded(items, onDrop, (item) => {
      const ordinary = Object.fromEntries(
        Object.entries(record(item[field]))
          .filter(([key]) => !trustedKeys.has(key))
          .slice(START, remaining),
      );
      item[field] = { ...ordinary, ...trusted };
      return item;
    });
  }
  function stampScope(value: unknown): FaroRecord {
    const scope = record(value);
    scope.spans = bounded(scope.spans, onDrop, (span) => {
      const ordinary = list(span.attributes)
        .filter((attribute) => !trustedKeys.has(String(record(attribute).key)))
        .slice(START, remaining);
      span.attributes = [...ordinary, ...spanAttributes];
      return span;
    });
    return scope;
  }
  function stampResource(value: unknown): FaroRecord {
    const resource = record(value);
    resource.scopeSpans = list(resource.scopeSpans).map((scope) => stampScope(scope));
    return resource;
  }
  return {
    logs: stamp(batch.logs, "context"),
    exceptions: stamp(batch.exceptions, "context"),
    measurements: stamp(batch.measurements, "context"),
    events: stamp(batch.events, "attributes"),
    traces: {
      resourceSpans: list(batch.traces.resourceSpans).map((resource) => stampResource(resource)),
    },
  };
}

export function faroEventCount(batch: FaroBatch): number {
  return (
    batch.logs.length +
    batch.exceptions.length +
    batch.measurements.length +
    batch.events.length +
    list(batch.traces.resourceSpans).reduce<number>(
      (total, resource) =>
        total +
        list(record(resource).scopeSpans).reduce<number>(
          (count, scope) => count + list(record(scope).spans).length,
          START,
        ),
      START,
    )
  );
}
