import { createFileRoute } from "@tanstack/react-router";

import { handleApiPassthrough } from "../../lib/api-passthrough";

/** `/connect/**` — the OIDC authorize/token/userinfo endpoints, proxied verbatim. */
export const Route = createFileRoute("/connect/$")({
  server: {
    handlers: {
      ANY: ({ request }): Promise<Response> => handleApiPassthrough(request),
    },
  },
});
