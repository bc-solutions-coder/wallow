/**
 * Browser logging with level filtering, bounded buffering, attribute redaction, and same-origin
 * batch delivery. Fetch failures go to the console and start a backoff window; failed batches
 * are not requeued. Browser instances register lifecycle listeners and an optional timer; other
 * runtimes flush only when called or when the buffer threshold is reached.
 */
import {
  DEFAULT_REDACT_KEYS,
  isAtLeast,
  redactAttrs,
  type LogBatch,
  type LogEvent,
  type LogEventError,
  type LogLevel,
} from "./log-event";

export {
  DEFAULT_INGEST_LIMITS,
  DEFAULT_REDACT_KEYS,
  isValidEventName,
  LOG_LEVELS,
  REDACTED,
  REQUEST_ID_HEADER,
  type IngestLimits,
  type LogBatch,
  type LogEvent,
  type LogEventError,
  type LogLevel,
} from "./log-event";

/** How a consumer configures the browser logger. */
export interface LoggerOptions {
  /**
   * What this logger is for, e.g. `"wallow-web"`.
   *
   * Deliberately NOT sent on the wire: the ingest handler stamps the service
   * name it was configured with, because a record that names its own service is
   * a record any page can forge. It shows up in the `console` fallback, which is
   * the only place the browser's copy is observable.
   */
  service: string;
  /** Same-origin path the batches are POSTed to (`/bff/logs`, `/logs`). */
  endpoint: string;
  /** Lowest level recorded. Anything below it is not buffered at all. Default `"info"`. */
  level?: LogLevel;
  /** The current `x-request-id`, when the app tracks one. Stamped on every event. */
  getCorrelationId?: () => string | undefined;
  /**
   * Read the current CSRF token for a protected ingest route. Fetch sends it in x-csrf-token;
   * beacon delivery puts it in the batch body.
   */
  getCsrfToken?: () => string | null;
  /** Attribute keys scrubbed before an event is buffered. Default {@link DEFAULT_REDACT_KEYS}. */
  redact?: readonly string[];
  /** Attributes stamped on every event from this logger. */
  attrs?: Record<string, unknown>;
  /** Buffer ceiling; the oldest events are dropped past it. Default 200. */
  maxBufferedEvents?: number;
  /** Buffer length that triggers a flush. Default 20. */
  flushAtEvents?: number;
  /** Interval flush period in ms. Default 5000. `0` disables the timer. */
  flushIntervalMs?: number;
  /** How long transport stays disabled after a failure, in ms. Default 30000. */
  transportBackoffMs?: number;
  /**
   * Fetch-batch size limit in UTF-8 bytes, defaulting to 65536. Oversized fetch batches are split;
   * the pagehide beacon path does not apply this split.
   */
  maxBodyBytes?: number;
}

/** The logger an app holds. */
export interface Logger {
  /**
   * Record a debug event when it meets the configured minimum level. Use a stable event name,
   * per-call attributes, and an optional error; per-call attributes override base attributes.
   */
  debug: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  /**
   * Record a info event when it meets the configured minimum level. Use a stable event name,
   * per-call attributes, and an optional error; per-call attributes override base attributes.
   */
  info: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  /**
   * Record a warn event when it meets the configured minimum level. Use a stable event name,
   * per-call attributes, and an optional error; per-call attributes override base attributes.
   */
  warn: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  /**
   * Record a error event when it meets the configured minimum level. Use a stable event name,
   * per-call attributes, and an optional error; per-call attributes override base attributes.
   */
  error: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  /** A logger stamping `attrs` on top of this one's. Shares the same buffer. */
  child: (attrs: Record<string, unknown>) => Logger;
  /**
   * Attempt to send buffered events unless transport is in backoff. Fetch-path failures are logged
   * and swallowed; failed events are not requeued.
   */
  flush: () => Promise<void>;
  /**
   * Stop the shared timer and remove lifecycle listeners for this logger and its children. Does
   * not flush the buffer or disable further recording.
   */
  dispose: () => void;
}

const DEFAULT_MAX_BUFFERED_EVENTS = 200;
const DEFAULT_FLUSH_AT_EVENTS = 20;
const DEFAULT_FLUSH_INTERVAL_MS = 5000;
const DEFAULT_TRANSPORT_BACKOFF_MS = 30_000;
const DEFAULT_MAX_BODY_BYTES = 65_536;

/** Nothing buffered, nothing dropped, no interval configured. */
const NONE = 0;
/** A batch of one cannot be halved. */
const UNSPLITTABLE = 1;
/** The divisor a split uses. */
const HALVES = 2;
/** The start of a slice. */
const FIRST_INDEX = 0;

/** The event a flush prepends when the buffer had to drop records. */
const DROPPED_EVENT: string = "logger.dropped";

/** Normalize whatever a caller passed as an error into the three fields that survive JSON. */
function toLogEventError(error: unknown): LogEventError | undefined {
  if (error === undefined || error === null) {
    return undefined;
  }
  if (error instanceof Error) {
    return {
      name: error.name,
      message: error.message,
      ...(error.stack === undefined ? {} : { stack: error.stack }),
    };
  }
  return { name: "NonError", message: String(error) };
}

/** Whether this runtime is a page: the only place listeners and a beacon exist. */
function inBrowser(): boolean {
  return typeof document !== "undefined";
}

/** UTF-8 byte length of a serialized batch. */
function byteLength(body: string): number {
  return new TextEncoder().encode(body).length;
}

/**
 * The mutable state one `createLogger` call owns. A `child()` shares it, which is
 * what makes a child's records interleave with its parent's in one batch rather
 * than racing two buffers to the same route.
 */
interface LoggerCore {
  readonly options: Required<
    Pick<
      LoggerOptions,
      | "service"
      | "endpoint"
      | "level"
      | "maxBufferedEvents"
      | "flushAtEvents"
      | "flushIntervalMs"
      | "transportBackoffMs"
      | "maxBodyBytes"
    >
  >;
  readonly redact: readonly string[];
  readonly getCorrelationId: (() => string | undefined) | undefined;
  readonly getCsrfToken: (() => string | null) | undefined;
  buffer: LogEvent[];
  dropped: number;
  disabledUntil: number;
  timer: ReturnType<typeof setInterval> | undefined;
  listeners: (() => void)[];
}

/** Take everything buffered, prepending the drop report when there is one. */
function drainBuffer(core: LoggerCore): LogEvent[] {
  const events: LogEvent[] = core.buffer;
  core.buffer = [];

  if (core.dropped === NONE) {
    return events;
  }

  const dropped: number = core.dropped;
  core.dropped = NONE;
  return [
    {
      ts: new Date().toISOString(),
      level: "warn",
      event: DROPPED_EVENT,
      attrs: { count: dropped },
    },
    ...events,
  ];
}

/**
 * Report a batch that could not be sent, and disable transport for the backoff
 * window.
 *
 * The events are NOT requeued. A requeue turns one unreachable ingest route into
 * a buffer that refills itself every backoff window and never drains, and the
 * records are not lost to the person who can act on them — they are on the
 * console, which is where a broken telemetry path belongs.
 */
function reportTransportFailure(core: LoggerCore, events: LogEvent[], cause: unknown): void {
  core.disabledUntil = Date.now() + core.options.transportBackoffMs;
  console.warn(`${core.options.service}: log transport failed, backing off`, cause, events);
}

/** POST one batch with `keepalive`, so a flush survives the navigation that triggered it. */
async function postBatch(core: LoggerCore, events: LogEvent[]): Promise<void> {
  const token: string | null = core.getCsrfToken?.() ?? null;
  const headers: Record<string, string> = { "content-type": "application/json" };
  if (token !== null && token !== "") {
    headers["x-csrf-token"] = token;
  }

  const batch: LogBatch = { events };
  const body: string = JSON.stringify(batch);

  if (byteLength(body) > core.options.maxBodyBytes) {
    await postSplit(core, events);
    return;
  }

  const response: Response = await fetch(core.options.endpoint, {
    method: "POST",
    keepalive: true,
    credentials: "same-origin",
    headers,
    body,
  });

  if (!response.ok) {
    throw new Error(`log ingest answered ${String(response.status)}`);
  }
}

/**
 * Halve an oversized batch and send each half.
 *
 * A single event that alone exceeds the cap is dropped and reported: it cannot
 * be split, and truncating it would ship a record whose meaning changed on the
 * way out.
 */
async function postSplit(core: LoggerCore, events: LogEvent[]): Promise<void> {
  if (events.length <= UNSPLITTABLE) {
    core.dropped += events.length;
    console.warn(`${core.options.service}: log record exceeds the body cap, dropped`, events);
    return;
  }

  const middle: number = Math.floor(events.length / HALVES);
  await postBatch(core, events.slice(FIRST_INDEX, middle));
  await postBatch(core, events.slice(middle));
}

/** Flush through `fetch`. Never rejects — a transport failure lands on the console. */
async function flushCore(core: LoggerCore): Promise<void> {
  if (core.buffer.length === NONE && core.dropped === NONE) {
    return;
  }
  if (Date.now() < core.disabledUntil) {
    return;
  }

  const events: LogEvent[] = drainBuffer(core);

  try {
    await postBatch(core, events);
  } catch (error: unknown) {
    reportTransportFailure(core, events, error);
  }
}

/**
 * Attempt a final beacon flush on pagehide.
 * The CSRF token travels in the body because sendBeacon cannot set custom headers.
 * Queue acceptance does not confirm delivery.
 */
function flushBeacon(core: LoggerCore): void {
  if (core.buffer.length === NONE && core.dropped === NONE) {
    return;
  }
  if (Date.now() < core.disabledUntil) {
    return;
  }

  const events: LogEvent[] = drainBuffer(core);
  const token: string | null = core.getCsrfToken?.() ?? null;
  const batch: LogBatch = {
    events,
    ...(token === null || token === "" ? {} : { csrfToken: token }),
  };
  const body: Blob = new Blob([JSON.stringify(batch)], { type: "application/json" });

  const queued: boolean = navigator.sendBeacon(core.options.endpoint, body);
  if (!queued) {
    console.warn(`${core.options.service}: terminal log flush was not queued`, events);
  }
}

/** Record one event, after the level filter and the client-side redaction pass. */
function record(
  core: LoggerCore,
  baseAttrs: Record<string, unknown>,
  level: LogLevel,
  event: string,
  attrs: Record<string, unknown> | undefined,
  error: unknown,
): void {
  if (!isAtLeast(level, core.options.level)) {
    return;
  }

  const correlationId: string | undefined = core.getCorrelationId?.();
  const normalizedError: LogEventError | undefined = toLogEventError(error);

  core.buffer.push({
    ts: new Date().toISOString(),
    level,
    event,
    attrs: redactAttrs({ ...baseAttrs, ...attrs }, core.redact),
    ...(correlationId === undefined ? {} : { correlationId }),
    ...(normalizedError === undefined ? {} : { error: normalizedError }),
  });

  while (core.buffer.length > core.options.maxBufferedEvents) {
    core.buffer.shift();
    core.dropped += 1;
  }

  if (core.buffer.length >= core.options.flushAtEvents) {
    void flushCore(core);
  }
}

/** Build the public logger over a core and a set of base attributes. */
function bindLogger(core: LoggerCore, baseAttrs: Record<string, unknown>): Logger {
  const at =
    (level: LogLevel) =>
    (event: string, attrs?: Record<string, unknown>, error?: unknown): void => {
      record(core, baseAttrs, level, event, attrs, error);
    };

  return {
    debug: at("debug"),
    info: at("info"),
    warn: at("warn"),
    error: at("error"),
    child: (attrs: Record<string, unknown>): Logger => bindLogger(core, { ...baseAttrs, ...attrs }),
    flush: (): Promise<void> => flushCore(core),
    dispose: (): void => {
      if (core.timer !== undefined) {
        clearInterval(core.timer);
        core.timer = undefined;
      }
      for (const off of core.listeners) {
        off();
      }
      core.listeners = [];
    },
  };
}

/**
 * Register the two page-lifecycle flushes.
 *
 * `visibilitychange` -> hidden is the one that carries the load: it fires on tab
 * switch, app switch and — on mobile — the discard path that never fires
 * `pagehide` at all. `pagehide` is the last call, and takes the beacon.
 */
function registerLifecycle(core: LoggerCore): void {
  const onHidden = (): void => {
    if (document.visibilityState === "hidden") {
      void flushCore(core);
    }
  };
  const onPageHide = (): void => {
    flushBeacon(core);
  };

  document.addEventListener("visibilitychange", onHidden);
  window.addEventListener("pagehide", onPageHide);

  core.listeners.push(
    (): void => {
      document.removeEventListener("visibilitychange", onHidden);
    },
    (): void => {
      window.removeEventListener("pagehide", onPageHide);
    },
  );
}

/**
 * Create a reusable logger with a shared buffer for its child loggers. Applies the level filter
 * and configured attribute redaction before enqueueing. Browser instances flush on a timer, hidden
 * visibility, and pagehide; dispose stops the timer and listeners without flushing.
 *
 * Fetch transport failures are logged and start a backoff window without requeueing the failed
 * batch. Supply nonthrowing callbacks and serializable attributes; record callbacks and the
 * pagehide beacon path can throw.
 */
export function createLogger(options: LoggerOptions): Logger {
  const core: LoggerCore = {
    options: {
      service: options.service,
      endpoint: options.endpoint,
      level: options.level ?? "info",
      maxBufferedEvents: options.maxBufferedEvents ?? DEFAULT_MAX_BUFFERED_EVENTS,
      flushAtEvents: options.flushAtEvents ?? DEFAULT_FLUSH_AT_EVENTS,
      flushIntervalMs: options.flushIntervalMs ?? DEFAULT_FLUSH_INTERVAL_MS,
      transportBackoffMs: options.transportBackoffMs ?? DEFAULT_TRANSPORT_BACKOFF_MS,
      maxBodyBytes: options.maxBodyBytes ?? DEFAULT_MAX_BODY_BYTES,
    },
    redact: options.redact ?? DEFAULT_REDACT_KEYS,
    getCorrelationId: options.getCorrelationId,
    getCsrfToken: options.getCsrfToken,
    buffer: [],
    dropped: 0,
    disabledUntil: 0,
    timer: undefined,
    listeners: [],
  };

  if (inBrowser()) {
    registerLifecycle(core);

    if (core.options.flushIntervalMs > NONE) {
      core.timer = setInterval((): void => {
        void flushCore(core);
      }, core.options.flushIntervalMs);
    }
  }

  return bindLogger(core, options.attrs ?? {});
}
