import { createFileRoute } from "@tanstack/react-router";

/** `/api/**` — the proxy that attaches the session's bearer and refreshes it silently. */
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
