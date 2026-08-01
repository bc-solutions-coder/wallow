import { handleLogIngest } from "@shared/lib/log-ingest.server";
import { createFileRoute } from "@tanstack/react-router";

/**
 * `POST /logs` — the browser logger's ingest route.
 *
 * A top-level path, not one under a `/bff` prefix: this app has no BFF. It is the
 * second mount point of the ONE ingest handler, and the whole difference between
 * the two apps is what each supplies to it — wallow-web a CSRF verifier and a
 * session context, this app neither.
 *
 * `POST` only, unlike the passthrough splats: there is no upstream whose method
 * contract a filter here could hide, and the ingest handler answers a `GET` with
 * the same 405 the router would.
 */
export const Route = createFileRoute("/logs")({
  server: {
    handlers: {
      POST: ({ request }): Promise<Response> => handleLogIngest(request),
    },
  },
});
