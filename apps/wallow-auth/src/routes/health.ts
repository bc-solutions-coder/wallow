import { createFileRoute } from "@tanstack/react-router";

/** Liveness body — unchanged from the deleted h3 host, which container healthchecks wait on. */
const HEALTH_BODY = "ready";

/**
 * `GET /health` — 200 `ready`, answered without touching the API or the router.
 *
 * Both compose stacks probe this path (`docker/docker-compose.test.yml`'s
 * `node -e "fetch('http://localhost:3002/health')"`), so the path and the body
 * are a container contract, not an internal detail.
 */
export const Route = createFileRoute("/health")({
  server: {
    handlers: {
      GET: (): Response => new Response(HEALTH_BODY),
    },
  },
});
