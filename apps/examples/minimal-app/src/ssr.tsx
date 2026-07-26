import { createRequestHandler, defaultRenderHandler } from "@tanstack/react-router/ssr/server";

import { createRouter } from "./router";

/**
 * Server-side render entry. Loaded through Vite's `ssrLoadModule` by
 * `dev-server.ts` (and bundled to `dist/server/ssr.js` for `pnpm start`) so the
 * whole render path — the router request handler, `RouterServer`, and the route
 * components — shares one Vite module graph and a single React instance.
 *
 * `createRequestHandler` drives the router for the incoming request (memory
 * history, `router.load()`, dehydrate) and `defaultRenderHandler` renders the
 * matched tree to an HTML `Response`.
 *
 * Kept as `.tsx` (not `.ts`) so it stays outside the app's typecheck include for
 * `.ts` files: like `router.tsx`/`routes/*.tsx`, it depends on the file-route
 * types the Start route-tree codegen produces. Vite/esbuild strip the types at
 * runtime, so this does not affect boot.
 */
export function render(request: Request): Promise<Response> {
  return createRequestHandler({ createRouter, request })(defaultRenderHandler);
}
