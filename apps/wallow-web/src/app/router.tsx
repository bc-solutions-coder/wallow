import {
  createQueryClient,
  failureReference,
  type QueryClient,
  resolveFailureMessagePrototype,
} from "@bc-solutions-coder/query";
import { toastFailure } from "@bc-solutions-coder/ui/failure-toast";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { setupRouterSsrQueryIntegration } from "@tanstack/react-router-ssr-query";
import { getGlobalStartContext } from "@tanstack/react-start";

import { routeTree } from "./routeTree.gen";

/**
 * Where this app's API surface is mounted, as the BROWSER addresses it: the BFF
 * `/api` proxy on this same origin. Spelled out rather than imported from the
 * SDK's `WALLOW_API_MOUNT`, which ships on the Node-only `./server` entry.
 */
const BROWSER_API_BASE_URL = "/api";

/**
 * The request's SDK off Start's global context, or `undefined` when there is no
 * request in scope. On the server `getGlobalStartContext()` THROWS rather than
 * answering `undefined` — once because no Start context is in the surrounding
 * `AsyncLocalStorage` at all (a spec calling `getRouter()` directly), and once
 * because the global middlewares have not run yet — and neither is an error
 * here: both mean "no request SDK to inherit", which is the browser's answer too.
 */
function readRequestSdk(): WallowSdk | undefined {
  try {
    return getGlobalStartContext()?.sdk;
  } catch {
    return undefined;
  }
}

/**
 * Build the router that boots the wallow-web app. Start calls this ONCE PER
 * REQUEST on the server (and once on the client), so everything it constructs is
 * request-scoped: a fresh `QueryClient` — never a module-global one, which would
 * hand one user's cached data to the next — and the request's own SDK instance.
 *
 * On the server that SDK comes from the global request middleware in `start.ts`,
 * the only place the inbound cookie is in scope; off a request (the browser, and
 * the specs that drive this factory directly) there is nothing to forward, so it
 * mints a same-origin instance against the BFF proxy. `globalThis.location` is
 * deliberately not consulted for that fallback: a relative base is what the
 * browser wants anyway, and Node — where the node-project route specs run — has
 * no `location` to read.
 *
 * `setupRouterSsrQueryIntegration` owns the dehydrate/hydrate handoff of the
 * React Query cache across the SSR boundary and installs the single
 * `QueryClientProvider` this app used to wire by hand through a `Wrap`
 * render-prop over a JSON-stringified `dehydrate()` payload. Loaders (router
 * context) and components (React Query hooks) therefore still share ONE cache
 * per request.
 *
 * The route tree is codegen'd into `src/routeTree.gen.ts` by the Start plugin as
 * a side effect of `vite dev` / `vite build`, so there is no `routes:generate`
 * step to remember.
 *
 * The return type is inferred deliberately: annotating it `AnyRouter` erases the
 * route-tree types that make `Link`/`useParams` typed.
 */
export function getRouter() {
  // PROTOTYPE (#168): every mutation failure no screen claimed becomes a toast.
  // Guarded so the per-request server-side client never raises one.
  const queryClient: QueryClient = createQueryClient({
    onUnhandledFailure: ({ error }): void => {
      if (typeof window === "undefined") {return;}
      toastFailure(resolveFailureMessagePrototype(error), failureReference(error));
    },
  });
  const sdk: WallowSdk = readRequestSdk() ?? createWallowSdk({ baseUrl: BROWSER_API_BASE_URL });

  const router = createTanStackRouter({
    routeTree,
    context: { queryClient, sdk },
    scrollRestoration: true,
  });

  setupRouterSsrQueryIntegration({ router, queryClient });

  return router;
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof getRouter>;
  }
}
