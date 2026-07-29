import { DEFAULT_SESSION_TTL_SECONDS } from "@bc-solutions-coder/sdk/server";
import { createFileRoute } from "@tanstack/react-router";

/**
 * `GET /health` — a server-only route whose sole job is to import the SDK's
 * `./server` subpath.
 *
 * That entry is the Node BFF surface (openid-client, iron-webcrypto, cookie-es),
 * so this route is what proves the packed tarball declares those runtime
 * dependencies: without them the Nitro server bundle fails to resolve, even
 * though nothing in the browser graph would notice.
 */
export const Route = createFileRoute("/health")({
  server: {
    handlers: {
      GET: (): Response => Response.json({ status: "ready", ttl: DEFAULT_SESSION_TTL_SECONDS }),
    },
  },
});
