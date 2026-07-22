import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { routeTree } from "./routeTree.gen";

/**
 * Constructs the TanStack router that boots the wallow-auth Start app.
 *
 * The route tree is produced by TanStack Router's file-based codegen
 * (`src/routeTree.gen.ts`, regenerated via `pnpm routes:generate`); every route
 * under `src/routes/` is wired into it automatically, so no route is bound by
 * hand here. The paths themselves are the app's external contract — the stable
 * auth URLs any client links to.
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
