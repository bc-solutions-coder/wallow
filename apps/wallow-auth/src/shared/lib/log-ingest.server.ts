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
import { createLogIngestHandler, type LogIngestHandler } from "@bc-solutions-coder/logger/server";
import {
  createClientAddressResolver,
  createRequestOriginResolver,
  type PeerRequest,
} from "@bc-solutions-coder/sdk/server/forwarded";

/** What this app calls itself in a record. Stamped server-side; the page never sends it. */
const SERVICE = "wallow-auth";

/**
 * This deployment's client-address and origin resolution, bound once at module
 * scope — parsing `WALLOW_TRUSTED_PROXIES` is start-up work, not per-record work.
 * Both are gated by that one list. With the variable unset the peer address srvx
 * read off the connection is the answer and no header is consulted at all.
 */
const clientAddressFor: (request: PeerRequest) => string | undefined = createClientAddressResolver(
  process.env,
);
const requestOriginFor: (request: PeerRequest) => string = createRequestOriginResolver(process.env);

/** Collector base URL. Unset — the default outside the compose stacks — logs to stdout. */
const otlpEndpoint: string = (process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? "").trim();

/**
 * The bound ingest handler.
 *
 * `allowedOrigins` resolves to the origin THIS request was addressed to, which
 * only a page actually served from this app can match — the Origin-versus-target
 * check. `requestOriginFor` honours `x-forwarded-proto` from a peer inside
 * `WALLOW_TRUSTED_PROXIES` (which production sets), so the check still holds
 * behind the TLS-terminating proxy this app runs behind there.
 *
 * `clientAddress` answers with the caller's address, which is the key the rate
 * limiter buckets on and the value stamped as `clientIp`. This route is
 * unauthenticated, so a header the caller writes would be a rate-limit bypass and
 * a forged field — which is why `clientAddressFor` reads the forwarded chain ONLY
 * when the peer is a proxy this deployment configured, and answers with the peer
 * itself otherwise. Unconfigured, it consults nothing inbound at all.
 */
const ingest: LogIngestHandler = createLogIngestHandler({
  service: SERVICE,
  allowedOrigins: (request: PeerRequest): string[] => [requestOriginFor(request)],
  clientAddress: clientAddressFor,
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
