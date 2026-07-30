import { createFileRoute } from "@tanstack/react-router";

/**
 * `GET /health` — 200 `{"status":"ok"}`, the body the deleted h3 host answered
 * with and the response both compose stacks probe (`docker-compose.test.yml`
 * runs `node -e "fetch('http://localhost:3000/health')"` and gates `depends_on`
 * on it), so path and shape are a container contract.
 *
 * It goes through the BFF server rather than answering a bare 200 on purpose:
 * building that server is what validates the OIDC configuration, and a host that
 * cannot serve `/bff/**` must not report itself healthy. Under the deleted host
 * the same misconfiguration crashed the process at import; here it surfaces as a
 * failed healthcheck instead.
 *
 * `src/app/lib/bff.server.ts` is imported lazily for the reason spelled out in
 * `src/routes/bff/$.ts`: it is Node-only, and route modules are reachable from
 * the client graph.
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
