import { createFileRoute } from "@tanstack/react-router";

/**
 * `POST /contact` — an end-user action with NO user signed in. The platform has
 * no anonymous API, so this reaches it as the deployment's registered service
 * account (a public site's contact form, exactly). Lazy import for the same
 * reason as the `/bff` and `/api` routes: the module behind it is Node-only.
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
