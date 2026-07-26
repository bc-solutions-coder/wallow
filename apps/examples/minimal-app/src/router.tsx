import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";

import { routeTree } from "./routeTree.gen";

// Side-effect import: registers the app's client configurator with the SDK query
// bootstrap. Loaded here so BOTH the client and SSR module graphs are armed
// before any route fires a query.
import "./lib/sdk";

/**
 * Constructs the TanStack router that boots the app. The route tree is produced
 * by TanStack Router's file-based codegen (`src/routeTree.gen.ts`, regenerated
 * via `pnpm routes:generate`), so every route under `src/routes/` is wired in
 * automatically. The React Query client comes from `@bc-solutions-coder/web-shell`.
 */
export function createRouter(): AnyRouter {
  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createRouter>;
  }
}
