import { QueryClient, QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * Single shared client (Wallow-evd5.3.4). The root route used to mint its OWN
 * QueryClient (a `useRef` client wrapped in a `QueryClientProvider`), so loaders
 * (router-context client) and components (root-route client) ran on two separate
 * caches. That second client goes away: the router owns the single provider via
 * its `Wrap`, and `RootComponent` renders the document shell directly. The
 * standalone shell therefore establishes no client of its own — a routed subtree
 * reads its QueryClient from whatever provider the caller supplies.
 *
 * We stand in for the routed subtree by mocking `<Outlet/>` with a probe that
 * consumes `useQueryClient()`: it throws "No QueryClient set" when no provider
 * sits above it, and otherwise records the client it actually resolved — which
 * is what distinguishes "the shell passes the caller's client through" from "the
 * shell shadows it with one of its own".
 *
 * Kept in a dedicated file (not `__root.test.tsx`) because this suite needs a
 * provider-consuming `Outlet` mock while that suite drives the real SSR entry —
 * `vi.mock` is hoisted per module, so the two cannot coexist.
 */
const probe = vi.hoisted(() => ({ client: undefined as unknown }));

vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => {
      // Throws unless a QueryClientProvider is an ancestor of the Outlet.
      probe.client = useQueryClient();
      return <div data-testid="outlet-with-query-client" />;
    },
  };
});

// The shell also mounts `<FocusOnNavigate/>` (from `@bc-solutions-coder/ui`),
// whose `useRouterState` call throws under a bare `renderToString(<Shell/>)`
// with no `RouterProvider`. This suite is about the query provider only, so it
// is a render-nothing sentinel here.
vi.mock("@bc-solutions-coder/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/ui")>();
  return {
    ...actual,
    FocusOnNavigate: () => null,
  };
});

describe("routes/__root (query provider)", () => {
  it("does not establish its own QueryClient (the router Wrap now owns it)", async () => {
    const { Route } = await import("./__root");
    const Shell = Route.options.component!;

    // With the standalone `useRef` client + provider removed, the shell renders
    // no provider of its own, so an Outlet that reads `useQueryClient()` throws
    // when the caller supplies none.
    expect(() => renderToString(<Shell />)).toThrow(/No QueryClient set/u);
  });

  it("passes the caller's QueryClient through to the routed subtree", async () => {
    const { Route } = await import("./__root");
    const Shell = Route.options.component!;

    const client = new QueryClient();
    probe.client = undefined;

    const html: string = renderToString(
      <QueryClientProvider client={client}>
        <Shell />
      </QueryClientProvider>,
    );

    expect(html).toContain('data-testid="outlet-with-query-client"');
    // The shell must not shadow the caller with a client of its own.
    expect(probe.client).toBe(client);
  });
});
