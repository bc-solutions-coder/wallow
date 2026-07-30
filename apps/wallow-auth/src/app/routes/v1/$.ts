import { createFileRoute } from "@tanstack/react-router";

import { handleApiPassthrough } from "@shared/lib/api-passthrough";

/**
 * `/v1/**` — the API surface, reverse-proxied verbatim to Wallow.Api.
 *
 * One `ANY` handler rather than a method map: the upstream owns which verbs a
 * path answers, so filtering here would turn an upstream 405 into a local 404
 * and hide the real contract.
 */
export const Route = createFileRoute("/v1/$")({
  server: {
    handlers: {
      ANY: ({ request }): Promise<Response> => handleApiPassthrough(request),
    },
  },
});
