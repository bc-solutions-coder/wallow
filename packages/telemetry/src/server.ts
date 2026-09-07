import type { TelemetryLogger } from "./logger-types.js";
import { readTelemetryConfig, type TelemetryOptions } from "./configuration.js";
import { fetchWithContext } from "./propagation.js";
import { LIMITS, NANOSECONDS_PER_MILLISECOND } from "./limits.js";
import { AsyncLocalStorage } from "node:async_hooks";
import { randomBytes } from "node:crypto";
import { createServerLogger } from "@bc-solutions-coder/logger/server";
import { monitorCrashes } from "./crash.js";
import { createExporter } from "./exporter.js";
import { sanitizeAttributes, sanitizeException } from "./privacy.js";

const SPAN_ERROR = 2;
const SPAN_OK = 1;
const SERVER_ERROR_STATUS = 500;
const START = 0;
const identifier = /^[A-Za-z0-9_.-]{1,128}$/u;

export type { ExportStats } from "./exporter.js";

export type { TelemetryLogger } from "./logger-types.js";

export type { TelemetryOptions } from "./configuration.js";

interface SpanContext {
  traceId: string;
  spanId: string;
}
const context = new AsyncLocalStorage<SpanContext & { failed: boolean }>();
function attributes(values: Record<string, unknown>) {
  return Object.entries(values).map(([key, value]) => {
    if (typeof value === "number") {
      return { key, value: { doubleValue: value } };
    }
    if (typeof value === "boolean") {
      return { key, value: { boolValue: value } };
    }
    return {
      key,
      value: { stringValue: typeof value === "string" ? value : JSON.stringify(value) },
    };
  });
}
function timestamp(): string {
  return (BigInt(Date.now()) * NANOSECONDS_PER_MILLISECOND).toString();
}
function parent(header: string | null): SpanContext | undefined {
  const parsed = /^00-(?<traceId>[a-f0-9]{32})-(?<spanId>[a-f0-9]{16})-0[01]$/u.exec(header ?? "");
  const traceId = parsed?.groups?.traceId;
  const spanId = parsed?.groups?.spanId;
  return traceId !== undefined &&
    spanId !== undefined &&
    !/^0+$/u.test(traceId) &&
    !/^0+$/u.test(spanId)
    ? { traceId, spanId }
    : undefined;
}

/** Initialize explicitly after loading server configuration. Importing this entry captures nothing. */
export function initializeTelemetry(options: TelemetryOptions = {}) {
  const { destination, credential, environment, release } = readTelemetryConfig(options);
  const exporter = createExporter({
    endpoint: destination.href.replace(/\/$/u, ""),
    credential,
    environment,
    release,
  });
  const ownedOrigins = new Set(options.ownedOrigins);
  const captured = new WeakSet<object>();
  const delivered = new WeakSet<object>();
  let capturing: object | undefined;
  const stopMonitoring = monitorCrashes({
    endpoint: destination.href.replace(/\/$/u, ""),
    credential,
    environment,
    release,
    delivered,
  });
  const resource = {
    attributes: attributes({
      "deployment.environment.name": environment,
      "service.version": release,
    }),
  };
  const core = createServerLogger({
    service: "registered-server",
    console: false,
    sink(records) {
      const current = context.getStore();
      const errorObject = capturing;
      exporter.enqueue(
        "logs",
        {
          resourceLogs: [
            {
              resource,
              scopeLogs: [
                {
                  scope: { name: "wallow.telemetry" },
                  logRecords: records.map((record) => ({
                    timeUnixNano: timestamp(),
                    severityText: record.level.toUpperCase(),
                    severityNumber: { debug: 5, info: 9, warn: 13, error: 17 }[record.level],
                    body: { stringValue: record.event },
                    attributes: attributes(sanitizeAttributes(record.attrs)),
                    ...(current === undefined
                      ? {}
                      : { traceId: current.traceId, spanId: current.spanId }),
                  })),
                },
              ],
            },
          ],
        },
        () => {
          if (errorObject !== undefined) {
            delivered.add(errorObject);
          }
        },
      );
    },
  });
  function logger(base: Record<string, unknown>): TelemetryLogger {
    function emit(
      level: "debug" | "info" | "warn" | "error",
      event: string,
      attrs?: Record<string, unknown>,
      error?: unknown,
    ): void {
      try {
        if (!/^[a-z][a-z0-9_.-]{0,127}$/u.test(event)) {
          return;
        }
        const values = sanitizeAttributes({ ...base, ...attrs });
        // Error objects never enter the logger's unsanitized exception serializer.
        core[level](event, { ...values, ...(error === undefined ? {} : sanitizeException(error)) });
      } catch {
        /* Telemetry cannot interrupt application work. */
      }
    }
    return {
      debug: (event, attrs, error) => emit("debug", event, attrs, error),
      info: (event, attrs, error) => emit("info", event, attrs, error),
      warn: (event, attrs, error) => emit("warn", event, attrs, error),
      error: (event, attrs, error) => emit("error", event, attrs, error),
      child: (attrs) => logger({ ...base, ...attrs }),
    };
  }
  const log = logger({});
  function captureException(error: unknown, expected = false): void {
    if (typeof error === "object" && error !== null) {
      if (captured.has(error)) {
        return;
      }
      captured.add(error);
    }
    capturing = typeof error === "object" && error !== null ? error : undefined;
    if (expected) {
      log.warn("exception.handled", {}, error);
    } else {
      log.error("exception.unexpected", {}, error);
    }
    capturing = undefined;
  }
  function trace<T>(
    name: string,
    operation: () => T | Promise<T>,
    incoming?: SpanContext,
  ): Promise<T> {
    const ancestor = incoming ?? context.getStore();
    const span = {
      traceId: ancestor?.traceId ?? randomBytes(LIMITS.traceBytes).toString("hex"),
      spanId: randomBytes(LIMITS.spanBytes).toString("hex"),
      failed: false,
    };
    const startTimeUnixNano = timestamp();
    const start = performance.now();

    return context.run(span, async () => {
      try {
        return await operation();
      } catch (error) {
        span.failed = true;
        captureException(error);
        throw error;
      } finally {
        exporter.enqueue("metrics", {
          resourceMetrics: [
            {
              resource,
              scopeMetrics: [
                {
                  metrics: [
                    {
                      name: "wallow.request.duration",
                      unit: "ms",
                      gauge: {
                        dataPoints: [
                          { timeUnixNano: timestamp(), asDouble: performance.now() - start },
                        ],
                      },
                    },
                  ],
                },
              ],
            },
          ],
        });
        exporter.enqueue("traces", {
          resourceSpans: [
            {
              resource,
              scopeSpans: [
                {
                  scope: { name: "wallow.telemetry" },
                  spans: [
                    {
                      traceId: span.traceId,
                      spanId: span.spanId,
                      ...(ancestor === undefined ? {} : { parentSpanId: ancestor.spanId }),
                      name: identifier.test(name) ? name : "request",
                      kind: 2,
                      startTimeUnixNano,
                      endTimeUnixNano: timestamp(),
                      status: { code: span.failed ? SPAN_ERROR : SPAN_OK },
                    },
                  ],
                },
              ],
            },
          ],
        });
      }
    });
  }
  return {
    logger: log,
    captureException,
    trace,
    instrument(
      handler: (request: Request) => Response | Promise<Response>,
      route: { route: string },
    ) {
      const name = route.route
        .replaceAll(/[^A-Za-z0-9_.-]/gu, ".")
        .slice(START, LIMITS.identityLength);
      return (request: Request): Promise<Response> =>
        trace(
          name,
          async () => {
            const response = await handler(request);
            if (response.status >= SERVER_ERROR_STATUS) {
              const active = context.getStore();
              if (active !== undefined) {
                active.failed = true;
              }
              log.error("request.failed", { "http.response.status_code": response.status });
            }
            return response;
          },
          parent(request.headers.get("traceparent")),
        );
    },
    fetch(input: string | URL | Request, init?: RequestInit): Promise<Response> {
      const request = new Request(input, init);
      const active = context.getStore();
      return fetchWithContext(
        request,
        ownedOrigins,
        active === undefined ? undefined : `00-${active.traceId}-${active.spanId}-01`,
        undefined,
        typeof init?.body === "string" ||
          init?.body instanceof URLSearchParams ||
          init?.body instanceof Blob
          ? init.body
          : undefined,
      );
    },
    stats: exporter.stats,
    flush: exporter.flush,
    async shutdown(): Promise<void> {
      stopMonitoring();
      await exporter.shutdown();
    },
  };
}

export { createBrowserRelay } from "./relay.js";
export type { BrowserRelayOptions, TelemetrySession } from "./relay.js";
