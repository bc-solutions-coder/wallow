import { createFileRoute } from "@tanstack/react-router";

import { handleApiPassthrough } from "@shared/lib/api-passthrough";

/**
 * `/connect/**` — the OpenIddict endpoints (authorize, token, logout, userinfo),
 * proxied verbatim so the browser talks to one origin for the whole handshake.
 *
 * The upstream's `Set-Cookie` headers come back untouched, which is what makes
 * the login cookies land on this origin rather than the API's.
 */
export const Route = createFileRoute("/connect/$")({
  server: {
    handlers: {
      ANY: ({ request }): Promise<Response> => handleApiPassthrough(request),
    },
  },
});
