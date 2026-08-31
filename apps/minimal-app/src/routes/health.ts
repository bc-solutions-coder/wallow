import { createFileRoute } from "@tanstack/react-router";

/**
 * `GET /health` — the container healthcheck target. Delegates to the BFF
 * preset's health handler (lazy import: the module behind it is Node-only), so
 * an environment the preset cannot boot from turns the container unhealthy.
 */
export const Route = createFileRoute("/health")({
  server: {
    handlers: {
      GET: async (): Promise<Response> => {
        const { handleHealthRequest } = await import("../lib/bff.server");
        return handleHealthRequest();
      },
    },
  },
});
