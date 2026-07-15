import { useQueryClient } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * Query provider wiring (Wallow-8w1h.3.1). The root route must wrap its SSR
 * document shell in a `QueryClientProvider` so every routed child can call
 * React Query hooks. We stand in for the routed subtree by mocking `<Outlet/>`
 * with a probe that consumes `useQueryClient()`: it resolves only when a
 * provider sits above it, so a shell that forgets the provider throws
 * "No QueryClient set" during `renderToString`.
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

describe("routes/__root (query provider)", () => {
  it("wraps the routed outlet in a QueryClientProvider", async () => {
    const { Route } = await import("./__root");
    const Shell = Route.options.component!;
    const html = renderToString(<Shell />);
    expect(html).toContain('data-testid="outlet-with-query-client"');
  });
});
