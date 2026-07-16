import { createRequestHandler, defaultRenderHandler } from "@tanstack/react-router/ssr/server";

import { createRouter } from "./router";

/**
 * Server-side render entry for the wallow-auth Start SSR shell
 * (Wallow-vec7.1.4).
 *
 * Loaded through Vite's `ssrLoadModule` by `dev-server.ts` so the whole render
 * path — the TanStack router-core request handler, `RouterServer`, and the
 * route components (`__root`/`index`) — shares one Vite module graph and, with
 * it, a single React instance. Rendering the router from a plain Node import
 * instead would risk a second React copy and a broken RouterProvider context.
 *
 * `createRequestHandler` drives the router for the incoming request (memory
 * history, `router.load()`, dehydrate) and `defaultRenderHandler` renders the
 * matched tree to an HTML `Response`. When the index route throws a redirect in
 * `beforeLoad`, `router.load()` surfaces it and the request handler returns an
 * HTTP redirect Response instead of markup — that is the `/` -> `/login`
 * contract this phase asserts.
 *
 * Kept as `.tsx` (not `.ts`) so it stays outside the app's typecheck include
 * (`src/**\/*.ts`): like `router.tsx`/`routes/*.tsx`, it depends on the file-
 * route types that only resolve once the Start route-tree codegen lands. Vite/
 * esbuild strip the types at runtime, so this does not affect boot.
 */
export function render(request: Request): Promise<Response> {
  return createRequestHandler({ createRouter, request })(defaultRenderHandler);
}
