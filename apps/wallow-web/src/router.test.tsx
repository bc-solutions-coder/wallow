import { QueryClient, useQueryClient } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { createRouter } from "./router";

/**
 * Boot wiring (Wallow-8w1h.2.2: the Start app boots). `createRouter()` must
 * assemble the root + index routes into a TanStack router so that "/" resolves.
 */
describe("createRouter (boot wiring)", () => {
  it("constructs a router without throwing", () => {
    expect(() => createRouter()).not.toThrow();
  });

  it("registers the index route at /", () => {
    const router = createRouter();
    expect(Object.keys(router.routesById)).toContain("/");
  });
});

/**
 * Query wiring (Wallow-8w1h.3.1). `createRouter()` must register a QueryClient
 * in the router context so route loaders/beforeLoad can reach it via
 * `context.queryClient`. The client comes from `createQueryClient()` and is
 * minted per router (per SSR request), so no arg is required at the call site.
 */
describe("createRouter (query context)", () => {
  it("exposes a QueryClient on the router context", () => {
    const router = createRouter();
    expect(router.options.context?.queryClient).toBeInstanceOf(QueryClient);
  });
});

/**
 * Single shared client (Wallow-evd5.2.4). Loaders reach the QueryClient through
 * the router context; components reach it through a `QueryClientProvider`. Those
 * must be the SAME instance per request, or SSR-prefetched loader data never
 * reaches the components that consume it. The router therefore owns the provider
 * via its `Wrap` render-prop, minting exactly one client that is both the context
 * client and the React-tree client — replacing the second `useRef` client that
 * `routes/__root.tsx` used to mint.
 */
describe("createRouter (single shared query client)", () => {
  it("wraps the app in a QueryClientProvider using the router-context client", () => {
    const router = createRouter();

    const Wrap = router.options.Wrap;
    if (Wrap === undefined) {
      throw new Error("router exposes no Wrap, so no QueryClientProvider spans the app");
    }

    let providedClient: QueryClient | undefined;
    function Probe(): null {
      providedClient = useQueryClient();
      return null;
    }

    renderToString(
      <Wrap>
        <Probe />
      </Wrap>,
    );

    expect(providedClient).toBe(router.options.context?.queryClient);
  });
});
