/**
 * Server log ingestion and direct server logging. Ingestion checks the Origin header, payload
 * limits, and a per-client rate limit before optional authorization. It stamps service, receipt
 * time, and host-supplied identity fields. Accepted batches return 204 even when their sink
 * fails.
 */
import {
  DEFAULT_INGEST_LIMITS,
  DEFAULT_REDACT_KEYS,
  isAtLeast,
  parseLogBatch,
  redactAttrs,
  REQUEST_ID_HEADER,
  type BatchResult,
  type IngestLimits,
  type LogBatch,
  type LogEvent,
  type LogLevel,
} from "./log-event";
import { emitOtlp, type OtlpEmitResult, type ServerLogRecord } from "./otlp";
import {
  createRateLimiter,
  DEFAULT_RATE_LIMIT,
  type RateLimitOptions,
  type RateLimiter,
} from "./rate-limit";

export {
  DEFAULT_INGEST_LIMITS,
  DEFAULT_REDACT_KEYS,
  isValidEventName,
  parseLogBatch,
  redactAttrs,
  REQUEST_ID_HEADER,
  type IngestLimits,
  type LogBatch,
  type LogEvent,
  type LogEventError,
  type LogLevel,
} from "./log-event";
export {
  emitOtlp,
  otlpLogsUrl,
  toOtlpLogsPayload,
  type OtlpEmitResult,
  type OtlpLogsPayload,
  type ServerLogRecord,
} from "./otlp";
export {
  createRateLimiter,
  DEFAULT_RATE_LIMIT,
  type RateLimiter,
  type RateLimitOptions,
} from "./rate-limit";

/** What the handler learned about the caller from the app's own session state. */
export interface LogRequestContext {
  /**
   * Authenticated user identifier supplied by the host context callback.
   */
  userId?: string;
  /**
   * Resolved tenant identifier supplied by the host context callback.
   */
  tenantId?: string;
}

/** Where accepted records go. Returning nothing is fine; throwing is not fatal. */
export type LogSink = (records: ServerLogRecord[]) => void | Promise<void>;

/** How an app configures its ingest route. */
export interface LogIngestOptions {
  /**
   * The service name stamped on every record — `"wallow-web"`, `"wallow-auth"`.
   * Taken from configuration, never from the payload.
   */
  service: string;
  /**
   * Exact allowed Origin values, including scheme, host, and port. An empty list or absent Origin
   * rejects the request. A callback can derive the list per request; forwarded values require the
   * host own trust policy.
   */
  allowedOrigins: readonly string[] | ((request: Request) => readonly string[]);
  /** Collector base URL, e.g. `http://localhost:4318`. Omitted: no OTLP emit. */
  otlpEndpoint?: string;
  /** Payload caps. Default {@link DEFAULT_INGEST_LIMITS}. */
  limits?: IngestLimits;
  /** Per-IP window. Default {@link DEFAULT_RATE_LIMIT}. */
  rateLimit?: RateLimitOptions;
  /** Attribute keys scrubbed server-side — the authoritative pass. */
  redact?: readonly string[];
  /**
   * Resolve a trusted client address from the host runtime. Used for the rate-limit key and
   * stamped clientIp. Missing or empty values put every such request in the unknown bucket.
   */
  clientAddress?: (request: Request) => string | undefined;
  /**
   * Optional authorization callback run after batch validation. For CSRF checks, account for the
   * request header and the beacon batch csrfToken. Returning false rejects with 403; omission adds
   * no session or CSRF check.
   */
  authorize?: (request: Request, batch: LogBatch) => boolean | Promise<boolean>;
  /** Session-derived fields stamped onto every record of this request. */
  context?: (request: Request) => LogRequestContext | Promise<LogRequestContext>;
  /** Override the destination. Default: OTLP when `otlpEndpoint` is set, else `console`. */
  sink?: LogSink;
  /** Clock seam. Default `Date.now`. */
  now?: () => number;
  /** `fetch` seam for the OTLP emit. */
  fetch?: typeof fetch;
}

/** The bound handler an app's route calls. */
export type LogIngestHandler = (request: Request) => Promise<Response>;

const STATUS_NO_CONTENT = 204;
const STATUS_BAD_REQUEST = 400;
const STATUS_FORBIDDEN = 403;
const STATUS_METHOD_NOT_ALLOWED = 405;
const STATUS_PAYLOAD_TOO_LARGE = 413;
const STATUS_TOO_MANY_REQUESTS = 429;

/** The rate-limit key every request shares when the host supplies no address. */
const UNKNOWN_CLIENT = "unknown";

/** UTF-8 byte length — `String.length` counts code units, and the cap is bytes. */
function byteLength(body: string): number {
  return new TextEncoder().encode(body).length;
}

/**
 * A rejection.
 *
 * The reason travels back to an unauthenticated caller, so every one of them
 * describes the shape of the request rather than anything about the server.
 */
function reject(status: number, reason: string): Response {
  return Response.json({ reason }, { status });
}

/** Whether the request's `Origin` is on the allowlist. */
function originAllowed(request: Request, allowed: LogIngestOptions["allowedOrigins"]): boolean {
  const origin: string | null = request.headers.get("origin");
  if (origin === null) {
    return false;
  }
  const list: readonly string[] = typeof allowed === "function" ? allowed(request) : allowed;

  return list.includes(origin);
}

/** The host's answer for this request, with an empty string read as no answer. */
function peerAddress(
  request: Request,
  resolve: LogIngestOptions["clientAddress"],
): string | undefined {
  const value: string | undefined = resolve?.(request);

  return value === undefined || value === "" ? undefined : value;
}

/** The console fallback, used when no collector is configured. */
function consoleSink(records: ServerLogRecord[]): void {
  for (const record of records) {
    // One JSON object per line: what a container's stdout collector expects.
    console.log(JSON.stringify(record));
  }
}

/** Turn one wire event into the record the server owns. */
function toServerRecord(
  event: LogEvent,
  options: {
    service: string;
    nowIso: string;
    redact: readonly string[];
    fallbackCorrelationId: string | undefined;
    clientIp: string | undefined;
    context: LogRequestContext;
  },
): ServerLogRecord {
  const correlationId: string | undefined = event.correlationId ?? options.fallbackCorrelationId;

  return {
    ts: options.nowIso,
    clientTs: event.ts,
    level: event.level,
    event: event.event,
    attrs: redactAttrs(event.attrs, options.redact),
    service: options.service,
    ...(correlationId === undefined ? {} : { correlationId }),
    ...(options.clientIp === undefined ? {} : { clientIp: options.clientIp }),
    ...(options.context.userId === undefined ? {} : { userId: options.context.userId }),
    ...(options.context.tenantId === undefined ? {} : { tenantId: options.context.tenantId }),
    ...(event.error === undefined ? {} : { error: event.error }),
  };
}

/**
 * Create one reusable POST handler with a persistent per-client rate limiter. Checks origin,
 * request rate, payload limits, and optional authorization before constructing server records.
 * Accepted batches return 204 even if the sink fails; rejected requests return 400, 403, 405, 413,
 * or 429 with a reason.
 *
 * Configure clientAddress from trusted runtime peer information. Without it, all requests share
 * the unknown bucket. Exceptions from application callbacks such as authorize and context
 * propagate.
 */
export function createLogIngestHandler(options: LogIngestOptions): LogIngestHandler {
  const limits: IngestLimits = options.limits ?? DEFAULT_INGEST_LIMITS;
  const redact: readonly string[] = options.redact ?? DEFAULT_REDACT_KEYS;
  const now: () => number = options.now ?? Date.now;
  const limiter: RateLimiter = createRateLimiter(options.rateLimit ?? DEFAULT_RATE_LIMIT);
  const sink: LogSink =
    options.sink ??
    (async (records: ServerLogRecord[]): Promise<void> => {
      if (options.otlpEndpoint === undefined) {
        consoleSink(records);
        return;
      }
      const result: OtlpEmitResult = await emitOtlp(
        options.otlpEndpoint,
        records,
        now(),
        options.fetch ?? fetch,
      );
      if (!result.ok) {
        // The collector is the thing that is down; the console is what is left.
        console.warn(`${options.service}: OTLP log emit failed`, result.status, result.error);
      }
    });

  return async (request: Request): Promise<Response> => {
    if (request.method !== "POST") {
      return reject(STATUS_METHOD_NOT_ALLOWED, "method not allowed");
    }
    if (!originAllowed(request, options.allowedOrigins)) {
      return reject(STATUS_FORBIDDEN, "origin not allowed");
    }
    // Resolved once: the limiter key and the stamped field are the same fact,
    // and a host that answered differently between them would be a bug.
    const clientIp: string | undefined = peerAddress(request, options.clientAddress);
    if (!limiter.allow(clientIp ?? UNKNOWN_CLIENT, now())) {
      return reject(STATUS_TOO_MANY_REQUESTS, "too many log batches");
    }

    const declared: string | null = request.headers.get("content-length");
    if (declared !== null && Number(declared) > limits.maxBodyBytes) {
      return reject(STATUS_PAYLOAD_TOO_LARGE, "batch is too large");
    }

    let body: string;
    try {
      body = await request.text();
    } catch {
      return reject(STATUS_BAD_REQUEST, "body could not be read");
    }
    if (byteLength(body) > limits.maxBodyBytes) {
      return reject(STATUS_PAYLOAD_TOO_LARGE, "batch is too large");
    }

    let decoded: unknown;
    try {
      decoded = JSON.parse(body);
    } catch {
      return reject(STATUS_BAD_REQUEST, "body is not JSON");
    }

    const parsed: BatchResult = parseLogBatch(decoded, limits);
    if (!parsed.ok) {
      return reject(STATUS_BAD_REQUEST, parsed.reason);
    }

    if (options.authorize !== undefined && !(await options.authorize(request, parsed.batch))) {
      return reject(STATUS_FORBIDDEN, "request is not authorized");
    }

    const context: LogRequestContext = (await options.context?.(request)) ?? {};
    const nowIso: string = new Date(now()).toISOString();
    const records: ServerLogRecord[] = parsed.batch.events.map(
      (event: LogEvent): ServerLogRecord =>
        toServerRecord(event, {
          service: options.service,
          nowIso,
          redact,
          fallbackCorrelationId: request.headers.get(REQUEST_ID_HEADER) ?? undefined,
          clientIp,
          context,
        }),
    );

    try {
      await sink(records);
    } catch (error: unknown) {
      // Deliberately not surfaced: the batch was valid and accepted, and a
      // failing collector is not the page's problem.
      console.warn(`${options.service}: log sink failed`, error);
    }

    return new Response(null, { status: STATUS_NO_CONTENT });
  };
}

/** How an app configures its own server-side logger. */
export interface ServerLoggerOptions {
  /** The service name stamped on every record. */
  service: string;
  /** Lowest level recorded. Default `"info"`. */
  level?: LogLevel;
  /** Collector base URL. Omitted: console only. */
  otlpEndpoint?: string;
  /** Attributes stamped on every record. */
  attrs?: Record<string, unknown>;
  /** Attribute keys scrubbed before a record leaves. */
  redact?: readonly string[];
  /** Override the destination. */
  sink?: LogSink;
  /**
   * Also write each record to the console.
   *
   * Default `true`, and it stays true even with a collector configured: in a
   * container, stdout is the one path that works when the collector does not,
   * and it is what `docker logs` shows.
   */
  console?: boolean;
  /** Clock seam. Default `Date.now`. */
  now?: () => number;
  /** `fetch` seam for the OTLP emit. */
  fetch?: typeof fetch;
}

/** The logger an app's server code holds. Fire-and-forget: nothing here awaits. */
export interface ServerLogger {
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
  /** A logger stamping `attrs` on top of this one's. */
  child: (attrs: Record<string, unknown>) => ServerLogger;
}

/** Normalize a thrown value into the three fields that survive JSON. */
function toRecordError(error: unknown): ServerLogRecord["error"] {
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

/**
 * Create a direct server logger with level filtering and attribute redaction. Writes to the
 * console by default and optionally invokes a custom sink or OTLP exporter. Child loggers overlay
 * base attributes.
 *
 * Delivery is not awaited. Rejected sink promises are logged, but a synchronous custom sink,
 * clock, or serialization failure can throw to the caller.
 */
export function createServerLogger(options: ServerLoggerOptions): ServerLogger {
  const minimum: LogLevel = options.level ?? "info";
  const redact: readonly string[] = options.redact ?? DEFAULT_REDACT_KEYS;
  const now: () => number = options.now ?? Date.now;
  const toConsole: boolean = options.console ?? true;

  const sink: LogSink =
    options.sink ??
    (async (records: ServerLogRecord[]): Promise<void> => {
      if (options.otlpEndpoint === undefined) {
        return;
      }
      const result: OtlpEmitResult = await emitOtlp(
        options.otlpEndpoint,
        records,
        now(),
        options.fetch ?? fetch,
      );
      if (!result.ok) {
        console.warn(`${options.service}: OTLP log emit failed`, result.status, result.error);
      }
    });

  const bind = (baseAttrs: Record<string, unknown>): ServerLogger => {
    const at =
      (level: LogLevel) =>
      (event: string, attrs?: Record<string, unknown>, thrown?: unknown): void => {
        if (!isAtLeast(level, minimum)) {
          return;
        }

        const iso: string = new Date(now()).toISOString();
        const normalizedError: ServerLogRecord["error"] = toRecordError(thrown);
        const record: ServerLogRecord = {
          ts: iso,
          clientTs: iso,
          level,
          event,
          attrs: redactAttrs({ ...baseAttrs, ...attrs }, redact),
          service: options.service,
          ...(normalizedError === undefined ? {} : { error: normalizedError }),
        };

        if (toConsole) {
          consoleSink([record]);
        }

        void Promise.resolve(sink([record])).catch((error: unknown): void => {
          console.warn(`${options.service}: log sink failed`, error);
        });
      };

    return {
      debug: at("debug"),
      info: at("info"),
      warn: at("warn"),
      error: at("error"),
      child: (attrs: Record<string, unknown>): ServerLogger => bind({ ...baseAttrs, ...attrs }),
    };
  };

  return bind(options.attrs ?? {});
}
