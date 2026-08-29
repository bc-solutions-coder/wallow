import { createFileRoute } from "@tanstack/react-router";

/** `/bff/**` — login, callback, user, logout, front-/back-channel logout. Path-only dispatch; the SDK owns method policy. */
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
