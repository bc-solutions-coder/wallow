/**
 * The ingest handler both apps mount, and the server-side logger they use for
 * their own records.
 *
 * **One transport, one record format, one handler, TWO mount points.**
 * `wallow-web` mounts it under `/bff/*`, where the SDK's CSRF gate already
 * applies; `wallow-auth` mounts it at `/logs` on its own Start server route and
 * supplies no CSRF verifier, because it holds no session and therefore has no
 * token to check. Neither app reimplements the guards.
 *
 * **CSRF is not the control this endpoint needs**, and treating it as one is how
 * an ingest route ends up unprotected in the app that has no session. The
 * controls that actually apply, in both apps:
 *
 *  - an **origin allowlist**, which is load-bearing rather than advisory:
 *    `Origin` is a forbidden header name, so page script cannot forge it, and it
 *    survives the `sendBeacon` path where no other header can be set;
 *  - **payload caps**, rejecting rather than truncating;
 *  - a **per-IP rate limit**, because the route is unauthenticated by design;
 *  - **server-side stamping** of receipt time, client IP, service, correlation id
 *    and tenant/user — every field a page could otherwise assert about itself.
 *
 * A valid batch answers **204 whether or not the collector accepted it**. The
 * page's behaviour must not change because telemetry is down.
 */
import {
  DEFAULT_CLIENT_IP_HEADER,
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
  DEFAULT_CLIENT_IP_HEADER,
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
  userId?: string;
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
   * Origins allowed to POST here, compared exactly (scheme, host and port).
   *
   * The load-bearing guard. An empty list rejects everything, which is the right
   * failure direction for a misconfigured deployment.
   *
   * A function is resolved per request, which is what lets an app whose own
   * public origin is not in its configuration — a pure passthrough app — answer
   * with the origin THIS request was addressed to and require the page to match
   * it. That is the classic Origin-versus-target check: the browser sets both,
   * and only a page actually served from this origin satisfies it.
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
  /** Header the host stamps the peer address onto. Default {@link DEFAULT_CLIENT_IP_HEADER}. */
  clientIpHeader?: string;
  /**
   * Whether this request may write logs, for apps that hold a session.
   *
   * Receives the parsed batch as well as the request, because on the
   * `sendBeacon` path the CSRF token is in the body — `sendBeacon` cannot set
   * headers, and the alternative is losing every log a closing tab was holding.
   * Omitted: no CSRF check, which is correct for a passthrough app.
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

/** The address a rate-limit key falls back to when no client IP was stamped. */
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

/** The rate-limit key: the stamped client address, or one shared bucket. */
function clientKey(request: Request, header: string): string {
  const value: string | null = request.headers.get(header);
  return value === null || value === "" ? UNKNOWN_CLIENT : value;
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
 * Build the ingest handler.
 *
 * A factory rather than a bare `handleLogIngest(request, options)` because the
 * rate limiter is state that must live ACROSS requests: a limiter constructed
 * per call counts to one and never refuses anything.
 */
export function createLogIngestHandler(options: LogIngestOptions): LogIngestHandler {
  const limits: IngestLimits = options.limits ?? DEFAULT_INGEST_LIMITS;
  const redact: readonly string[] = options.redact ?? DEFAULT_REDACT_KEYS;
  const clientIpHeader: string = options.clientIpHeader ?? DEFAULT_CLIENT_IP_HEADER;
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
    if (!limiter.allow(clientKey(request, clientIpHeader), now())) {
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
    const clientIp: string | null = request.headers.get(clientIpHeader);
    const nowIso: string = new Date(now()).toISOString();
    const records: ServerLogRecord[] = parsed.batch.events.map(
      (event: LogEvent): ServerLogRecord =>
        toServerRecord(event, {
          service: options.service,
          nowIso,
          redact,
          fallbackCorrelationId: request.headers.get(REQUEST_ID_HEADER) ?? undefined,
          clientIp: clientIp === null || clientIp === "" ? undefined : clientIp,
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
  debug: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  info: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
  warn: (event: string, attrs?: Record<string, unknown>, error?: unknown) => void;
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
 * Build the server-side logger.
 *
 * Same record shape as the browser's, so a request that starts in the page and
 * finishes on the server produces two records a collector can join on
 * `wallow.correlation_id` — which is the entire reason this package owns both
 * ends rather than leaving the server on `console.*`.
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
