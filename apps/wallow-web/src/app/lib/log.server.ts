/**
 * wallow-web's server-side logger.
 *
 * Its own module rather than a second export from `log-ingest.server.ts` because
 * `bff.server.ts` logs through it and the ingest module reads the BFF's session
 * store: putting both in one file makes those two modules a cycle.
 *
 * Records here carry the same shape the browser posts, so a request that starts
 * in the page and finishes on the server produces two records a collector joins
 * on the correlation id.
 */
import { createServerLogger, type ServerLogger } from "@bc-solutions-coder/logger/server";

/** What this app calls itself in a record. Stamped server-side; the page never sends it. */
export const SERVICE = "wallow-web";

/**
 * Collector base URL.
 *
 * Unset — the default outside the compose stacks — means records go to stdout as
 * one JSON object per line, which is what `docker logs` shows and what a
 * container's log collector reads. `OTEL_EXPORTER_OTLP_ENDPOINT` is the standard
 * OpenTelemetry variable name, so the otel-lgtm stack in `docker/` needs no
 * Wallow-specific configuration.
 */
export const OTLP_ENDPOINT: string = (process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? "").trim();

export const serverLog: ServerLogger = createServerLogger({
  service: SERVICE,
  ...(OTLP_ENDPOINT === "" ? {} : { otlpEndpoint: OTLP_ENDPOINT }),
});
