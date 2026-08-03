import { createFileRoute } from "@tanstack/react-router";

import { handleApiPassthrough } from "../../lib/api-passthrough";

/**
 * `/.well-known/**` — OIDC discovery and JWKS, proxied verbatim: the documents
 * the API publishes advertise URLs on THIS origin, so they have to resolve here.
 *
 * The `[.]` in the directory name is the route-codegen escape for a leading dot;
 * a literal `.well-known/` directory would be read as a route-path separator.
 */
export const Route = createFileRoute("/.well-known/$")({
  server: {
    handlers: {
      ANY: ({ request }): Promise<Response> => handleApiPassthrough(request),
    },
  },
});
