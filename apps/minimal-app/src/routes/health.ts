import { createFileRoute } from "@tanstack/react-router";

/** Liveness body — unchanged from the deleted h3 host, which container healthchecks wait on. */
const HEALTH_BODY = "ready";

/** `GET /health` — 200 `ready`, answered without touching the API or the router. */
export const Route = createFileRoute("/health")({
  server: {
    handlers: {
      GET: (): Response => new Response(HEALTH_BODY),
    },
  },
});
