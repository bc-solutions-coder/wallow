/**
 * The app's one browser logger.
 *
 * A module singleton, because a second `createLogger` is a second buffer with its
 * own timer racing the same route for no gain. Import `log` — never call
 * `createLogger` in a screen.
 *
 * No `getCsrfToken`: this app holds no session, so there is no token to send and
 * the ingest route asks for none. The endpoint is prefixed with this build's base
 * path for the same reason every anchor here is — under a path-based ingress the
 * site root is a DIFFERENT app, so a literal `/logs` would POST the batch at
 * wallow-web.
 *
 * Events are NAMED, not prose: `login.failed`, `mfa.enrollment.expired`. Free
 * text belongs in the attribute bag; the ingest route rejects a batch whose event
 * name is not a dotted low-cardinality name.
 */
import { createLogger, type Logger } from "@bc-solutions-coder/logger";

import { toAppHref } from "./base-path";

export const log: Logger = createLogger({
  service: "wallow-auth",
  endpoint: toAppHref("/logs"),
});
