/**
 * wallow-web's log ingest route and its server-side logger.
 *
 * The guards, the caps, the limiter, the stamping and the OTLP encoding all live
 * in `@bc-solutions-coder/logger/server`; what a HOST still owns is the four
 * things only it can answer — which origin its own pages are served from, who the
 * peer is, whether this request holds a valid session, and who that session
 * belongs to.
 *
 * The handler is built ONCE at module scope, not per request: the rate limiter is
 * state that must live across requests, and a limiter constructed per call counts
 * to one and never refuses anything.
 */
import { type PeerRequest } from "@bc-solutions-coder/env/client-address";
import { createRequestOriginResolver } from "@bc-solutions-coder/env/request-origin";
import {
  createLogIngestHandler,
  type LogBatch,
  type LogIngestHandler,
  type LogRequestContext,
} from "@bc-solutions-coder/logger/server";
import { csrfTokenMatches, readSession, type BffSession } from "@bc-solutions-coder/sdk/server";

import { getBffServer } from "./bff.server";
import { clientAddressFor } from "./client-address.server";
import { OTLP_ENDPOINT, SERVICE } from "./log.server";

/**
 * This deployment's origin resolution, bound once — the same shape as
 * `clientAddressFor`, and gated by the same `WALLOW_TRUSTED_PROXIES` list:
 * `x-forwarded-proto` is believed only from a peer inside it.
 */
const requestOriginFor: (request: PeerRequest) => string = createRequestOriginResolver(process.env);

/** The session behind this request, or `null` when there is none. */
async function sessionFor(request: Request): Promise<BffSession | null> {
  const server = await getBffServer();

  return readSession(request, server.config, server.store);
}

/**
 * Whether this request may write logs.
 *
 * The route lives under `/bff`, so it is held to the same CSRF contract as every
 * other write there — but the token is read from the header OR the body, because
 * the terminal `pagehide` flush travels by `sendBeacon`, which cannot set
 * headers. An anonymous page has no session and therefore no token to present,
 * and it stays able to log: `csrfTokenMatches(undefined, …)` is false by
 * construction, so the check is skipped rather than inverted.
 */
async function authorizeLogBatch(request: Request, batch: LogBatch): Promise<boolean> {
  const session: BffSession | null = await sessionFor(request);
  if (session?.csrfToken === undefined) {
    return true;
  }

  const presented: string | undefined =
    request.headers.get("x-csrf-token") ?? batch.csrfToken ?? undefined;

  return csrfTokenMatches(session.csrfToken, presented);
}

/** The session-derived fields stamped onto every record of this request. */
async function contextFor(request: Request): Promise<LogRequestContext> {
  const session: BffSession | null = await sessionFor(request);
  if (session === null) {
    return {};
  }

  return {
    userId: session.user.sub,
    ...(session.user.tenantId === undefined ? {} : { tenantId: session.user.tenantId }),
  };
}

/**
 * The bound ingest handler.
 *
 * `allowedOrigins` resolves to the origin THIS request was addressed to, which
 * only a page actually served from this app can match — the Origin-versus-target
 * check, with no environment variable to get wrong in a fork.
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
  ...(OTLP_ENDPOINT === "" ? {} : { otlpEndpoint: OTLP_ENDPOINT }),
  authorize: authorizeLogBatch,
  context: contextFor,
});

/** Handle `POST /bff/logs` — one batch of browser records. */
export function handleLogIngest(request: PeerRequest): Promise<Response> {
  return ingest(request);
}
