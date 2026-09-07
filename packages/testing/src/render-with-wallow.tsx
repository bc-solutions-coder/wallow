/**
 * Browser-only rendering with a memory router, QueryClientProvider, and SDK harness.
 * Import from @bc-solutions-coder/testing/render-with-wallow.
 */
import {
  createQueryClient,
  type CreateQueryClientOptions,
  type QueryClient,
  QueryClientProvider,
  type UnhandledFailure,
} from "@bc-solutions-coder/query";
import {
  type AnyRoute,
  type AnyRouter,
  createMemoryHistory,
  createRootRouteWithContext,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import type { ReactElement, ReactNode } from "react";
import { render } from "vitest-browser-react";

import { createSdkHarness, type SdkHarness } from "./sdk-harness";

/** Path the memory history starts on when a spec does not name one. */
export const DEFAULT_RENDER_PATH = "/";

/**
 * Router context every `renderWithWallow` route tree is typed against — the
 * shape wallow-web's `sdkMiddleware` already supplies in production, so a spec
 * and the running app agree on what `beforeLoad`/`loader` can read.
 */
export interface WallowTestRouterContext {
  sdk: SdkHarness["sdk"];
  queryClient: QueryClient;
}

/**
 * A route plus the path to mount it at.
 *
 * `createFileRoute("/login")` routes carry NO `path` of their own — the generated
 * `routeTree.gen.ts` binds it with `.update({ path })` at build time, and a spec
 * that imports the route module directly gets nothing. A route with neither a
 * path nor an id is treated as a root, so mounted bare it takes the id
 * `__root__` and the router rejects the tree with "Duplicate routes found with
 * id: __root__". Naming the path here is what `routeTree.gen.ts` does in the app.
 */
export interface RouteMount {
  /** Where to mount it — the same string the route's `createFileRoute` was given. */
  readonly path: string;
  /** The route module's exported `Route`. */
  readonly route: AnyRoute;
}

/** A route that already knows its own path, or one that needs to be told. */
export type MountableRoute = AnyRoute | RouteMount;

/** Options for {@link renderWithWallow}. */
export interface RenderWithWallowOptions {
  /** Reuse an existing harness (e.g. one programmed in `beforeEach`). One is created otherwise. */
  harness?: SdkHarness | undefined;
  /** Reuse an existing query client. {@link createTestQueryClient} is used otherwise. */
  queryClient?: QueryClient | undefined;
  /**
   * Hears every failure the query client leaves to the app — the production
   * toast path. A spec that leaves a mutation to the toast asserts the call
   * lands here; one that renders the failure itself asserts it never does.
   * Cannot be combined with `queryClient` — that client owns its callback, and a
   * collector that never fires would pass a "stays quiet" assertion vacuously.
   */
  onUnhandledFailure?: ((failure: UnhandledFailure) => void) | undefined;
  /**
   * Additional context providers needed by the screen — an
   * app's failure-message registry, say. Applied inside the query client and
   * outside the router, so the wrapped tree sees the cache and the providers
   * see nothing route-specific.
   */
  wrap?: ((tree: ReactElement) => ReactElement) | undefined;
  /** Initial memory-history location. Defaults to {@link DEFAULT_RENDER_PATH}. */
  path?: string | undefined;
  /**
   * Real app routes to mount under the throwaway root, for specs that need the
   * route's own `validateSearch`/`beforeLoad`/`loader` rather than a bare
   * component. When omitted, `ui` renders directly under the root route.
   *
   * A file route must be given as a {@link RouteMount} so it has a path to
   * answer on; a route built with `createRoute({ path })` can be passed as is.
   */
  routes?: readonly MountableRoute[] | undefined;
  /**
   * Extra options for the throwaway root route itself — typically a `loader`
   * (with `loaderDeps`) whose data a mounted route's hooks read back via
   * `useLoaderData({ from: "__root__" })`, the way an app's real root feeds
   * layout-level state to its screens. Loosely typed on purpose: the root is
   * created here, so a spec cannot name its type, and TanStack validates the
   * options at runtime. `component` and `notFoundComponent` are supplied by this helper
   * and cannot be overridden.
   */
  rootOptions?: Record<string, unknown> | undefined;
}

/** What {@link renderWithWallow} returns: the render result plus the router, cache, and harness it uses. */
export type RenderWithWallowResult = ReturnType<typeof render> & {
  /** The harness governing this render's I/O — program it, then assert on `calls`. */
  readonly harness: SdkHarness;
  /** The query cache backing this render. */
  readonly queryClient: QueryClient;
  /** The memory router driving this render. */
  readonly router: AnyRouter;
};

/**
 * Create a fresh query client using the package's normal cache and error handling.
 *
 * Queries and mutations do not retry by default. Pass onUnhandledFailure to observe
 * failures not handled by a component.
 */
export function createTestQueryClient(options: CreateQueryClientOptions = {}): QueryClient {
  return createQueryClient(options);
}

/**
 * Render a component with a memory router, query cache, and SDK harness.
 *
 * Returns the browser render result plus harness, queryClient, and router. Program
 * the harness before rendering components that fetch on mount. Supplied routes
 * are reparented in place; file routes need a RouteMount with their path.
 *
 * @throws Error when both queryClient and onUnhandledFailure are supplied.
 */
export function renderWithWallow(
  ui: ReactNode,
  options: RenderWithWallowOptions = {},
): RenderWithWallowResult {
  const harness: SdkHarness = options.harness ?? createSdkHarness();
  if (options.queryClient !== undefined && options.onUnhandledFailure !== undefined) {
    throw new Error(
      "renderWithWallow: pass either queryClient or onUnhandledFailure — a supplied client owns its own callback.",
    );
  }
  const queryClient: QueryClient =
    options.queryClient ??
    createTestQueryClient(
      options.onUnhandledFailure === undefined
        ? {}
        : { onUnhandledFailure: options.onUnhandledFailure },
    );
  const wrap = options.wrap ?? ((tree: ReactElement): ReactElement => tree);

  // `ui` renders beside the `Outlet` rather than inside a child route, so a spec
  // can mount a bare component AND start the history anywhere it likes: the root
  // route matches every location, a child route (when `routes` is given) fills
  // the outlet, and neither case needs the caller to invent a path.
  const rootRoute = createRootRouteWithContext<WallowTestRouterContext>()({
    ...options.rootOptions,
    component: () => (
      <>
        {ui}
        <Outlet />
      </>
    ),
    // A bare `ui` render at any path other than "/" leaves the outlet unmatched.
    // Without this, TanStack Router warns and drops its default `<p>Not Found</p>`
    // into the tree — noise in the console and a stray paragraph a consumer
    // spec's `getByText` could trip over.
    notFoundComponent: () => null,
  });

  const router = createRouter({
    routeTree: rootRoute.addChildren(
      (options.routes ?? []).map((entry) => mount(entry, rootRoute)),
    ),
    history: createMemoryHistory({ initialEntries: [options.path ?? DEFAULT_RENDER_PATH] }),
    context: { sdk: harness.sdk, queryClient },
  });

  const result = render(
    <QueryClientProvider client={queryClient}>
      {wrap(<RouterProvider router={router} />)}
    </QueryClientProvider>,
  );

  return Object.assign(result, { harness, queryClient, router });
}

function isRouteMount(entry: MountableRoute): entry is RouteMount {
  return "route" in entry;
}

/**
 * Reparent a supplied route in place and assign the path of a RouteMount.
 * File routes obtain their paths from the generated app route tree, so tests
 * importing them directly must provide that path themselves.
 */
function mount(entry: MountableRoute, parent: AnyRoute): AnyRoute {
  const route: AnyRoute = isRouteMount(entry) ? entry.route : entry;

  route.options.getParentRoute = () => parent;

  if (isRouteMount(entry)) {
    Object.assign(route.options, { path: entry.path });
  }

  return route;
}
