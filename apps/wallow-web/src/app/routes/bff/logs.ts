import { createFileRoute } from "@tanstack/react-router";

/**
 * `POST /bff/logs` — the browser logger's ingest route.
 *
 * It sits under `/bff` deliberately: that is where this app's write contract
 * already lives, so the batch is held to the same CSRF check as every other write
 * here. A static route out-ranks the `/bff/$` splat beside it, so the tunnel's
 * 404-for-unknown-sub-paths behaviour is unchanged.
 *
 * `POST` only — no `ANY` handler. Unlike the tunnel there is no method contract
 * to surface: nothing here answers a `GET` with anything a caller can use, and
 * the ingest handler's own 405 is the same answer the router would give.
 *
 * `src/app/lib/log-ingest.server.ts` is reached through a dynamic import for the
 * reason `/bff/$` is: it pulls in `@bc-solutions-coder/sdk/server` (node:crypto,
 * openid-client) and every route module is a member of the route tree the CLIENT
 * graph imports.
 */
export const Route = createFileRoute("/bff/logs")({
  server: {
    handlers: {
      POST: async ({ request }): Promise<Response> => {
        const { handleLogIngest } = await import("../../lib/log-ingest.server");
        return handleLogIngest(request);
      },
    },
  },
});
