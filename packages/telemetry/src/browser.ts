import {
  BaseTransport,
  getTransportBody,
  initializeFaro,
  InternalLoggerLevel,
  LogLevel,
  PerformanceInstrumentation,
  WebVitalsInstrumentation,
  type TransportItem,
} from "@grafana/faro-web-sdk";
import { createBrowserQueue } from "./browser-queue.js";
import { BROWSER_LIMITS } from "./faro-privacy.js";
import { sanitizeAttributes } from "./privacy.js";
import { NANOSECONDS_PER_MILLISECOND } from "./limits.js";
import type { ExportStats } from "./exporter.js";
import type { TelemetryLogger } from "./logger-types.js";

const START = 0;
const TRACE_BYTES = 16;
const SPAN_BYTES = 8;
const HEX_BASE = 16;
const HEX_WIDTH = 2;
const CLIENT_SPAN = 3;
const ERROR_STATUS = 2;
const OK_STATUS = 1;
const SERVER_ERROR = 500;
const identifier = /^[A-Za-z][A-Za-z0-9_.-]{0,127}$/u;
export interface BrowserTelemetryOptions {
  relayPath: string;
  ownedOrigins?: readonly string[];
}
export interface BrowserTelemetry {
  ready: Promise<void>;
  logger: TelemetryLogger;
  measure: (event: string, value: number) => void;
  captureException: (error: unknown, expected?: boolean) => void;
  fetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
  resetContext: () => Promise<void>;
  flush: () => Promise<void>;
  dispose: () => Promise<void>;
  stats: () => ExportStats;
}
function id(bytes: number): string {
  return Array.from(crypto.getRandomValues(new Uint8Array(bytes)), (value) =>
    value.toString(HEX_BASE).padStart(HEX_WIDTH, "0"),
  ).join("");
}
function nano(): string {
  return (BigInt(Date.now()) * NANOSECONDS_PER_MILLISECOND).toString();
}
function attributes(value: unknown): Record<string, string> {
  return Object.fromEntries(
    Object.entries(sanitizeAttributes(value)).map(([key, item]) => [
      key,
      typeof item === "string" ? item : JSON.stringify(item),
    ]),
  );
}

// Faro's performance observers have document lifetime; reuse them across explicit sessions.
let performanceFaro: ReturnType<typeof initializeFaro> | undefined;
let activeSink: ((items: TransportItem | TransportItem[]) => void) | undefined;
let initializedRelay: string | undefined;
class RelayTransport extends BaseTransport {
  readonly name = "wallow.same-origin-relay";
  readonly version = "0.1.0";
  getIgnoreUrls(): string[] {
    return initializedRelay === undefined ? [] : [initializedRelay];
  }
  send(items: TransportItem | TransportItem[]): void {
    activeSink?.(items);
  }
}

/** Explicit browser initialization. Call resetContext after login, logout, or organization selection. */
export function initializeBrowserTelemetry(options: BrowserTelemetryOptions): BrowserTelemetry {
  const relay = new URL(options.relayPath, location.href);
  if (relay.origin !== location.origin || relay.search !== "" || relay.hash !== "") {
    throw new Error("Telemetry relay must be a same-origin path");
  }
  if (activeSink !== undefined) {
    throw new Error("Browser telemetry is already initialized");
  }
  if (initializedRelay !== undefined && initializedRelay !== relay.href) {
    throw new Error("Browser telemetry relay cannot change during the document lifetime");
  }
  initializedRelay = relay.href;
  const queue = createBrowserQueue(relay);
  const ready = queue.resetContext();
  let disposed = false;
  const captured = new WeakSet<object>();
  activeSink = (items) => {
    queue.enqueue(getTransportBody(Array.isArray(items) ? items : [items]));
  };
  performanceFaro ??= initializeFaro({
    app: { name: "wallow-browser" },
    isolate: false,
    preventGlobalExposure: true,
    internalLoggerLevel: InternalLoggerLevel.OFF,
    batching: { enabled: false },
    dedupe: false,
    instrumentations: [new PerformanceInstrumentation(), new WebVitalsInstrumentation()],
    transports: [new RelayTransport()],
    metas: [],
  });
  const faro = performanceFaro;
  faro.unpause();
  function captureException(error: unknown, expected = false): void {
    if (disposed) {
      return;
    }
    if (error !== null && typeof error === "object") {
      if (captured.has(error)) {
        return;
      }
      captured.add(error);
    }
    if (expected) {
      faro.api.pushLog(["application.failure.handled"], { level: LogLevel.WARN });
    } else {
      faro.api.pushError(error instanceof Error ? error : new Error("Unknown application failure"));
    }
  }
  function logger(extra: Record<string, unknown> = {}): TelemetryLogger {
    function log(
      level: LogLevel,
      event: string,
      attrs?: Record<string, unknown>,
      error?: unknown,
    ): void {
      if (disposed || !identifier.test(event)) {
        return;
      }
      faro.api.pushLog([event], {
        level,
        context: attributes({ ...extra, ...attrs }),
        skipDedupe: true,
      });
      if (error !== undefined) {
        captureException(error);
      }
    }
    return {
      debug: (event, attrs, error) => {
        log(LogLevel.DEBUG, event, attrs, error);
      },
      info: (event, attrs, error) => {
        log(LogLevel.INFO, event, attrs, error);
      },
      warn: (event, attrs, error) => {
        log(LogLevel.WARN, event, attrs, error);
      },
      error: (event, attrs, error) => {
        log(LogLevel.ERROR, event, attrs, error);
      },
      child: (attrs) => logger({ ...extra, ...attrs }),
    };
  }
  const onError = (event: ErrorEvent): void => {
    captureException(event.error);
  };
  const onRejection = (event: PromiseRejectionEvent): void => {
    captureException(event.reason);
  };
  const onPageHide = (): void => {
    void queue.flush();
  };
  globalThis.addEventListener("error", onError);
  globalThis.addEventListener("unhandledrejection", onRejection);
  globalThis.addEventListener("pagehide", onPageHide);
  const timer = setInterval(() => {
    void queue.flush();
  }, BROWSER_LIMITS.flushMs);
  const nativeFetch = globalThis.fetch.bind(globalThis);
  const ownedOrigins = new Set([location.origin, ...(options.ownedOrigins ?? [])]);
  async function instrumentedFetch(
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> {
    const request = new Request(
      input instanceof Request ? input : new URL(String(input), location.href),
      init,
    );
    request.headers.delete("baggage");
    request.headers.delete("tracestate");
    request.headers.delete("traceparent");
    const traceId = id(TRACE_BYTES);
    const spanId = id(SPAN_BYTES);
    const owned = ownedOrigins.has(new URL(request.url).origin) && request.url !== relay.href;
    if (owned && request.redirect === "error") {
      request.headers.set("traceparent", `00-${traceId}-${spanId}-01`);
    }
    const started = nano();
    const epoch = queue.epoch();
    let failed = false;
    try {
      const response = await nativeFetch(request);
      failed = response.status >= SERVER_ERROR;
      if (!disposed && failed && epoch === queue.epoch()) {
        faro.api.pushLog(["browser.request.failed"], {
          level: LogLevel.ERROR,
          spanContext: { traceId, spanId },
        });
      }
      return response;
    } catch (error) {
      failed = true;
      if (epoch === queue.epoch()) {
        captureException(error);
      } else if (error !== null && typeof error === "object") {
        captured.add(error);
      }
      throw error;
    } finally {
      if (epoch !== queue.epoch()) {
        queue.drop();
      }
      if (!disposed && owned && epoch === queue.epoch()) {
        faro.api.pushTraces({
          resourceSpans: [
            {
              resource: { attributes: [], droppedAttributesCount: START },
              scopeSpans: [
                {
                  scope: { name: "wallow.browser" },
                  spans: [
                    {
                      traceId,
                      spanId,
                      name: "browser.request",
                      kind: CLIENT_SPAN,
                      startTimeUnixNano: started,
                      endTimeUnixNano: nano(),
                      attributes: [],
                      events: [],
                      links: [],
                      droppedAttributesCount: START,
                      droppedEventsCount: START,
                      droppedLinksCount: START,
                      status: { code: failed ? ERROR_STATUS : OK_STATUS },
                    },
                  ],
                },
              ],
            },
          ],
        });
      }
    }
  }
  return {
    ready,
    logger: logger(),
    captureException,
    fetch: instrumentedFetch,
    measure(event, value) {
      if (!disposed && identifier.test(event) && Number.isFinite(value)) {
        faro.api.pushMeasurement({ type: event, values: { value } });
      }
    },
    resetContext: queue.resetContext,
    flush: queue.flush,
    stats: queue.stats,
    async dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      clearInterval(timer);
      globalThis.removeEventListener("error", onError);
      globalThis.removeEventListener("unhandledrejection", onRejection);
      globalThis.removeEventListener("pagehide", onPageHide);
      faro.pause();
      activeSink = undefined;
      await queue.flush();
      queue.clear();
    },
  };
}
