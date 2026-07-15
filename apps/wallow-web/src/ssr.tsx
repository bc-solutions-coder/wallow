import { createRequestHandler, defaultRenderHandler } from "@tanstack/react-router/ssr/server";

import { createRouter } from "./router";

/**
 * Server-side render entry for the TanStack Start SSR shell (Wallow-8w1h.2.2).
 *
 * Loaded through Vite's `ssrLoadModule` by `dev-server.ts` so the whole render
 * path — the TanStack router-core request handler, `RouterServer`, and the
 * route components (`__root`/`index`) — shares one Vite module graph and, with
 * it, a single React instance. Rendering the router from a plain Node import
 * instead would risk a second React copy and a broken RouterProvider context.
 *
 * `createRequestHandler` drives the router for the incoming request (memory
 * history, `router.load()`, dehydrate) and `defaultRenderHandler` renders the
 * matched tree to an HTML `Response` via `react-dom/server`. This is the same
 * pipeline TanStack Start uses; the home route's server-rendered markup carries
 * `data-testid="home-heading"`, which `curl /` asserts against.
 *
 * Kept as `.tsx` (not `.ts`) so it stays outside the app's typecheck include
 * (`src/**\/*.ts`): like `router.tsx`/`routes/*.tsx`, it depends on the file-
 * route types that only resolve once the Start route-tree codegen lands (task
 * 1.3). Vite/esbuild strip the types at runtime, so this does not affect boot.
 */
export function render(request: Request): Promise<Response> {
  return createRequestHandler({ createRouter, request })(defaultRenderHandler);
}
