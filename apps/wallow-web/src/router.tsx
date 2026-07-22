import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { routeTree } from "./routeTree.gen";

/**
 * Constructs the TanStack router that boots the wallow-web Start app.
 *
 * The route tree is produced by TanStack Router's file-based codegen
 * (`src/routeTree.gen.ts`, regenerated via `pnpm routes:generate`); every route
 * under `src/routes/` — including the `/dashboard` layout shell and the verticals
 * nested beneath it — is wired into it automatically, so no route is reparented
 * by hand here.
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
