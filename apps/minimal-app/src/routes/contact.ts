import { createFileRoute } from "@tanstack/react-router";

/**
 * PROTOTYPE — `POST /contact`: an end-user action with NO user signed in.
 * The platform has no anonymous API, so this reaches it as the registered
 * service account (bcordes.dev's contact form, exactly).
 */
export const Route = createFileRoute("/contact")({
  server: {
    handlers: {
      POST: async ({ request }): Promise<Response> => {
        const { submitInquiry } = await import("../lib/service-client.server");
        return submitInquiry(request);
      },
    },
  },
});
