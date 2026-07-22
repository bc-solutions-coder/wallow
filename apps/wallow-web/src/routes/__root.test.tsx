import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * The root route owns the SSR document shell (Wallow-8w1h.2.2). TanStack's
 * <Outlet/> cannot render outside a RouterProvider, so we replace it with a
 * sentinel and assert the shell markup the green phase must emit: a full
 * <html>/<head>/<body> document that mounts the outlet.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => <div data-testid="router-outlet" />,
  };
});

// The shell mounts `<FocusOnNavigate/>` (from `@bc-solutions-coder/ui`), which
// calls `useRouterState` — that throws under a bare `renderToString(<Shell/>)`
// with no `RouterProvider`. Its focus behaviour is the primitive's own spec's
// job (and `__root.focus.test.tsx`'s); here it is a render-nothing sentinel so
// the shell renders standalone. `useRouterState` stays real above.
vi.mock("@bc-solutions-coder/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/ui")>();
  return {
    ...actual,
    FocusOnNavigate: () => null,
  };
});

describe("routes/__root (SSR document shell)", () => {
  it("exposes a root route component", async () => {
    const { Route } = await import("./__root");
    expect(Route.options.component).toBeDefined();
  });

  it("server-renders a full html document shell mounting the outlet", async () => {
    const { Route } = await import("./__root");
    const Shell = Route.options.component!;
    const html = renderToString(<Shell />);
    expect(html).toContain("<html");
    expect(html).toContain("<body");
    expect(html).toContain('data-testid="router-outlet"');
  });
});
