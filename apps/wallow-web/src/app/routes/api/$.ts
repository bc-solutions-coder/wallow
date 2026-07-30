import { createFileRoute } from "@tanstack/react-router";

/**
 * `/api/**` — the reverse proxy onto the downstream API.
 *
 * Unlike wallow-auth's verbatim passthrough, this is the BFF's own proxy: it
 * resolves the session the login callback wrote, attaches the bearer token
 * server-side and refreshes it silently, so the browser never holds a token. The
 * proxy strips the `/api` prefix itself, so the whole subtree routes here
 * untouched.
 *
 * One `ANY` handler rather than a method map: the upstream owns which verbs a
 * path answers, so filtering here would turn an upstream 405 into a local 404.
 *
 * `src/app/lib/bff.server.ts` is imported lazily for the reason spelled out in
 * `src/routes/bff/$.ts`: it is Node-only, and route modules are reachable from
 * the client graph.
 */
export const Route = createFileRoute("/api/$")({
  server: {
    handlers: {
      ANY: async ({ request }): Promise<Response> => {
        const { handleApiRequest } = await import("../../lib/bff.server");
        return handleApiRequest(request);
      },
    },
  },
});
