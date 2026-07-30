import { createFileRoute } from "@tanstack/react-router";

/**
 * `/bff/**` — the OIDC tunnel: `login`, `callback`, `user`, `logout`.
 *
 * One `ANY` handler rather than a method map. Method policy belongs to the SDK's
 * handlers, which answer a bare `GET /bff/logout` with `405` + `Allow: POST`; a
 * method-filtered route would swallow that as a 404 and hide the contract. Sub-
 * paths the tunnel does not own answer 404 from the handler itself.
 *
 * `src/app/lib/bff.server.ts` is reached through a dynamic import, not a top-level one: it
 * pulls in `@bc-solutions-coder/sdk/server` (node:crypto, openid-client), and
 * every route module is a member of the route tree the CLIENT graph imports.
 * The Start plugin strips server handlers from the client bundle, but nothing
 * else that walks the tree does — importing it lazily keeps the Node-only host
 * out of every such graph instead of relying on that one transform.
 */
export const Route = createFileRoute("/bff/$")({
  server: {
    handlers: {
      ANY: async ({ request }): Promise<Response> => {
        const { handleBffRequest } = await import("../../lib/bff.server");
        return handleBffRequest(request);
      },
    },
  },
});
