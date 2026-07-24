import { QueryClient, QueryClientProvider, useQueryClient } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * Single shared client (Wallow-evd5.2.4). The root route used to mint its OWN
 * QueryClient (a `useRef` client wrapped in a `QueryClientProvider`), which meant
 * loaders (router-context client) and components (root-route client) ran on two
 * separate caches. That second client is removed: the router now owns the single
 * provider via its `Wrap`, and `RootComponent` renders the document shell
 * directly. So the standalone shell no longer establishes a client of its own —
 * a routed subtree reads its QueryClient from whatever provider the caller (the
 * router `Wrap`) supplies.
 *
 * We stand in for the routed subtree by mocking `<Outlet/>` with a probe that
 * consumes `useQueryClient()`: it resolves only when a provider sits above it,
 * and throws "No QueryClient set" when none does.
 *
 * Kept in a dedicated file (not `__root.test.tsx`) because this suite needs a
 * provider-consuming `Outlet` mock, whereas the shell suite mocks a plain
 * sentinel — `vi.mock` is hoisted per module, so the two mocks cannot coexist.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => {
      // Throws unless a QueryClientProvider is an ancestor of the Outlet.
      useQueryClient();
      return <div data-testid="outlet-with-query-client" />;
    },
  };
});

// The shell also mounts `<FocusOnNavigate/>` (from `@bc-solutions-coder/ui`),
// whose `useRouterState` call throws under a bare `renderToString(<Shell/>)`
// with no `RouterProvider`. This suite is about the query provider only, so it
// is a render-nothing sentinel here; its wiring lives in `__root.focus.test.tsx`.
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

  it("server-renders the shell when the caller supplies a QueryClientProvider", async () => {
    const { Route } = await import("./__root");
    const Shell = Route.options.component!;

    const client = new QueryClient();
    const html = renderToString(
      <QueryClientProvider client={client}>
        <Shell />
      </QueryClientProvider>,
    );

    expect(html).toContain('data-testid="outlet-with-query-client"');
  });
});
