import { createHmac, randomBytes, timingSafeEqual } from "node:crypto";
import { readTelemetryConfig, type TelemetryOptions } from "./configuration.js";
import {
  BROWSER_LIMITS,
  record,
  sanitizeFaroBatch,
  sanitizedPage,
  stampFaroContext,
} from "./faro-privacy.js";

const HTTP = {
  forbidden: 403,
  method: 405,
  rate: 429,
  unavailable: 503,
  media: 415,
  large: 413,
  bad: 400,
  changed: 409,
  accepted: 204,
};
const START = 0;
const ONE = 1;
const COOKIE_BYTES = 16;
const MILLISECONDS_PER_SECOND = 1000;
const RATE = 10;
const BURST = 20;
const cookieName = "wallow_telemetry_anon";
const identifier = /^[A-Za-z0-9_.-]{1,128}$/u;
export interface TelemetrySession {
  userId: string;
  organizationId?: string;
  sessionId: string;
}
export interface BrowserRelayOptions extends TelemetryOptions {
  origin: string;
  /** Resolve a validated server session. Never read identity from telemetry payloads or untrusted headers. */
  resolveSession: (request: Request) => Promise<TelemetrySession | undefined>;
}

function equal(left: string, right: string): boolean {
  const a = Buffer.from(left);
  const b = Buffer.from(right);
  return a.length === b.length && timingSafeEqual(a, b);
}
async function body(request: Request): Promise<string | undefined> {
  if (request.body === null) {
    return undefined;
  }
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let size = START;
  let expired = false;
  const timer = setTimeout(() => {
    expired = true;
    void reader.cancel();
  }, BROWSER_LIMITS.flushMs);
  try {
    while (true) {
      if (expired) {
        break;
      }
      // Read incrementally so chunked requests cannot bypass the byte limit.
      // eslint-disable-next-line no-await-in-loop
      const next = await reader.read();
      if (next.done) {
        break;
      }
      size += next.value.byteLength;
      if (size > BROWSER_LIMITS.batchBytes) {
        // Cancel the stream before releasing its lock.
        // eslint-disable-next-line no-await-in-loop
        await reader.cancel();
        return undefined;
      }
      chunks.push(next.value);
    }
    return expired ? undefined : Buffer.concat(chunks).toString("utf8");
  } finally {
    clearTimeout(timer);
    reader.releaseLock();
  }
}

/** Build once per application process. The browser never receives the ingestion credential. */
export function createBrowserRelay(options: BrowserRelayOptions) {
  const config = readTelemetryConfig(options);
  const origin = new URL(options.origin).origin;
  let concurrent = START;
  let tokens = BURST;
  let updated = Date.now();
  const stats = { accepted: START, rejected: START, dropped: START, failed: START };
  function hmac(value: string): string {
    return createHmac("sha256", config.credential)
      .update(`wallow.telemetry.context:${value}`)
      .digest("base64url");
  }
  function anonymous(request: Request): { id: string; cookie?: string } {
    const value =
      request.headers
        .get("cookie")
        ?.split(";")
        .map((part) => part.trim())
        .find((part) => part.startsWith(`${cookieName}=`))
        ?.slice(cookieName.length + ONE) ?? "";
    const [id, expiry, signature] = value.split(".");
    if (
      id !== undefined &&
      /^[a-f0-9]{32}$/u.test(id) &&
      expiry !== undefined &&
      /^\d{10,16}$/u.test(expiry) &&
      signature !== undefined &&
      Number(expiry) > Date.now() &&
      equal(signature, hmac(`${id}.${expiry}`))
    ) {
      return { id };
    }
    const fresh = randomBytes(COOKIE_BYTES).toString("hex");
    const expires = Date.now() + BROWSER_LIMITS.sessionSeconds * MILLISECONDS_PER_SECOND;
    const secure = origin.startsWith("https:") ? "; Secure" : "";
    return {
      id: fresh,
      cookie: `${cookieName}=${fresh}.${expires}.${hmac(`${fresh}.${expires}`)}; Path=/; HttpOnly; SameSite=Strict; Max-Age=${BROWSER_LIMITS.sessionSeconds}${secure}`,
    };
  }
  function reject(status: number): Response {
    stats.rejected += ONE;
    return new Response(null, { status });
  }
  async function handle(request: Request): Promise<Response> {
    const suppliedOrigin = request.headers.get("origin");
    if (
      new URL(request.url).origin !== origin ||
      (request.method === "POST"
        ? suppliedOrigin !== origin
        : suppliedOrigin !== null && suppliedOrigin !== origin)
    ) {
      return reject(HTTP.forbidden);
    }
    if (request.method !== "POST" && request.method !== "GET") {
      return reject(HTTP.method);
    }
    const now = Date.now();
    tokens = Math.min(BURST, tokens + ((now - updated) / MILLISECONDS_PER_SECOND) * RATE);
    updated = now;
    if (tokens < ONE || concurrent >= BROWSER_LIMITS.concurrent) {
      return reject(HTTP.rate);
    }
    tokens -= ONE;
    concurrent += ONE;
    try {
      const session = await options.resolveSession(request);
      if (
        session !== undefined &&
        (!identifier.test(session.userId) ||
          !identifier.test(session.sessionId) ||
          (session.organizationId !== undefined && !identifier.test(session.organizationId)))
      ) {
        return reject(HTTP.unavailable);
      }
      const anon = anonymous(request);
      const context = hmac(
        JSON.stringify([anon.id, session?.userId, session?.organizationId, session?.sessionId]),
      );
      const headers = new Headers({ "cache-control": "no-store" });
      if (anon.cookie !== undefined) {
        headers.set("set-cookie", anon.cookie);
      }
      if (request.method === "GET") {
        return Response.json({ context }, { headers });
      }
      if (request.headers.has("content-encoding")) {
        return reject(HTTP.media);
      }
      if (!request.headers.get("content-type")?.startsWith("application/json")) {
        return reject(HTTP.media);
      }
      if (Number(request.headers.get("content-length")) > BROWSER_LIMITS.batchBytes) {
        return reject(HTTP.large);
      }
      const raw = await body(request);
      if (raw === undefined) {
        return reject(HTTP.large);
      }
      let payload: Record<string, unknown>;
      try {
        payload = record(JSON.parse(raw));
      } catch {
        return reject(HTTP.bad);
      }
      if (typeof payload.context !== "string" || !equal(payload.context, context)) {
        stats.dropped += ONE;
        return Response.json({ context }, { status: HTTP.changed, headers });
      }
      const sessionId = hmac(session?.sessionId ?? anon.id);
      const trusted: Record<string, string> = { "session.id": sessionId };
      if (session !== undefined) {
        trusted["user.id"] = session.userId;
      }
      if (session?.organizationId !== undefined) {
        trusted["organization.id"] = session.organizationId;
      }
      const sanitized = stampFaroContext(
        sanitizeFaroBatch(payload, (count) => {
          stats.dropped += count;
        }),
        trusted,
        (count) => {
          stats.dropped += count;
        },
      );
      const meta = {
        app: { environment: config.environment, version: config.release },
        session: { id: sessionId },
        user:
          session === undefined
            ? undefined
            : {
                id: session.userId,
                attributes:
                  session.organizationId === undefined
                    ? {}
                    : { organization_id: session.organizationId },
              },
        page: {
          url: sanitizedPage(
            record(payload.meta).page === undefined
              ? undefined
              : record(record(payload.meta).page).url,
          ),
        },
      };
      const wire = JSON.stringify({ ...sanitized, meta });
      if (Buffer.byteLength(wire) > BROWSER_LIMITS.batchBytes) {
        return reject(HTTP.large);
      }
      try {
        const forwarded = await fetch(new URL("/faro", config.destination), {
          method: "POST",
          headers: {
            "content-type": "application/json",
            authorization: `Bearer ${config.credential}`,
            "x-wallow-environment": config.environment,
            "x-wallow-release": config.release,
          },
          body: wire,
          signal: AbortSignal.timeout(BROWSER_LIMITS.flushMs),
          redirect: "error",
        });
        await forwarded.body?.cancel();
        if (!forwarded.ok) {
          stats.failed += ONE;
          stats.dropped += ONE;
        } else {
          stats.accepted += ONE;
        }
      } catch {
        stats.failed += ONE;
        stats.dropped += ONE;
      }
      return new Response(null, { status: HTTP.accepted, headers });
    } catch {
      stats.failed += ONE;
      return new Response(null, { status: HTTP.unavailable });
    } finally {
      concurrent -= ONE;
    }
  }
  return { handle, stats: () => ({ ...stats }) };
}
