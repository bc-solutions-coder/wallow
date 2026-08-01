/**
 * wallow-auth's log ingest route and its server-side logger.
 *
 * The SAME handler wallow-web mounts, from `@bc-solutions-coder/logger/server` —
 * one transport, one record format, one handler, two mount points. What differs
 * is what this app can answer: it is a pure passthrough proxy holding no session,
 * so it supplies **no `authorize` verifier and no context**. There is no token to
 * check and no user to name, and pretending otherwise is how an ingest route ends
 * up with a CSRF check that always passes standing in for a real control.
 *
 * The guard that actually protects it is the origin check, which does not depend
 * on a session: `Origin` is a forbidden header name, so page script cannot forge
 * it, and it survives the `sendBeacon` path where no other header can be set.
 *
 * Built ONCE at module scope — the rate limiter is state that must live across
 * requests.
 */
import { resolveRequestOrigin } from "@bc-solutions-coder/env/request-origin";
import { createLogIngestHandler, type LogIngestHandler } from "@bc-solutions-coder/logger/server";

/**
 * The inbound request as srvx hands it to a Start server route. A WHATWG
 * `Request` has no socket, so the peer address arrives on this extra `ip`
 * property (populated in `vite dev` and in the built Nitro server alike).
 */
interface PeerRequest extends Request {
  readonly ip?: string | undefined;
}

/** What this app calls itself in a record. Stamped server-side; the page never sends it. */
const SERVICE = "wallow-auth";

/** Collector base URL. Unset — the default outside the compose stacks — logs to stdout. */
const otlpEndpoint: string = (process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? "").trim();

/**
 * The bound ingest handler.
 *
 * `allowedOrigins` resolves to the origin THIS request was addressed to, which
 * only a page actually served from this app can match — the Origin-versus-target
 * check. `resolveRequestOrigin` honours `x-forwarded-proto`, so the check still
 * holds behind the TLS-terminating proxy this app runs behind in production.
 *
 * `clientAddress` answers with the address srvx read off the connection, and it
 * is the ONLY source of the peer for both the rate-limit key and the stamped
 * `clientIp`. Nothing inbound is consulted: this route is unauthenticated, so a
 * header the caller writes would be a rate-limit bypass and a forged field.
 */
const ingest: LogIngestHandler = createLogIngestHandler({
  service: SERVICE,
  allowedOrigins: (request: Request): string[] => [resolveRequestOrigin(request)],
  clientAddress: (request: PeerRequest): string | undefined => request.ip,
  ...(otlpEndpoint === "" ? {} : { otlpEndpoint }),
});

/**
 * Handle `POST /logs` — one batch of browser records.
 *
 * There is deliberately no `createServerLogger` beside it: nothing in this app's
 * server code records anything today, and a logger with no caller is a seam that
 * rots. Add it here when the first server-side record appears.
 */
export function handleLogIngest(request: PeerRequest): Promise<Response> {
  return ingest(request);
}
