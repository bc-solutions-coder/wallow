/**
 * The app's one browser logger.
 *
 * A module singleton, because a second `createLogger` is a second buffer with its
 * own timer racing the same route for no gain. Import `log` — never call
 * `createLogger` in a screen.
 *
 * Events are NAMED, not prose: `bff.logout.failed`, `demo.request.failed`. Free
 * text belongs in the attribute bag, where it costs one attribute rather than one
 * series; the ingest route rejects a batch whose event name is not a dotted
 * low-cardinality name.
 */
import { createLogger, type Logger } from "@bc-solutions-coder/logger";
import { readCsrfCookie } from "@bc-solutions-coder/sdk";

/** Where the ingest route is mounted. Same-origin: the page never talks to a collector. */
const LOG_ENDPOINT = "/bff/logs";

export const log: Logger = createLogger({
  service: "wallow-web",
  endpoint: LOG_ENDPOINT,
  // The BFF's double-submit cookie is the one token source — the same source
  // the SDK's own request interceptor reads.
  getCsrfToken: readCsrfCookie,
});
