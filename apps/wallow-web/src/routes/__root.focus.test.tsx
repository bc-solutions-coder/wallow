import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

/**
 * The document shell's a11y focus-management contract (Wallow-0q2s.9.1). Mirror
 * of wallow-auth's shell, which mounts `<FocusOnNavigate/>` at the root so
 * *every* route moves keyboard/screen-reader focus to the destination screen's
 * `<h1>` after each client-side navigation. The current wallow-web
 * shell mounts no such component, so assistive tech never announces the new
 * screen on navigation — this spec pins that gap.
 *
 * `FocusOnNavigate` (from `@bc-solutions-coder/ui`, moved there in W3) renders
 * nothing of its own and calls `useRouterState`, which throws outside a
 * `RouterProvider`; like `ReadyIndicator` in the hydration spec it is mocked to
 * a sentinel, so the point proven here is that the shell *renders* it in the
 * right place, not that its focus effect runs (that is the primitive's own
 * `focus-on-navigate.test.tsx`'s job). TanStack's `<Outlet/>` is likewise a
 * sentinel so the shell renders standalone.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    Outlet: () => <div data-testid="router-outlet" />,
  };
});

vi.mock("@bc-solutions-coder/ui", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/ui")>();
  return {
    ...actual,
    FocusOnNavigate: () => <div data-testid="web-focus-on-navigate" />,
  };
});

async function renderShell(): Promise<string> {
  const { Route } = await import("./__root");
  const Shell = Route.options.component!;
  return renderToString(<Shell />);
}

describe("routes/__root (focus management)", () => {
  it("mounts FocusOnNavigate so every route manages focus on navigation", async () => {
    const html: string = await renderShell();

    expect(html).toContain('data-testid="web-focus-on-navigate"');
  });

  it("renders FocusOnNavigate ahead of the routed outlet, as the auth shell does", async () => {
    const html: string = await renderShell();

    const focusAt: number = html.indexOf('data-testid="web-focus-on-navigate"');
    const outletAt: number = html.indexOf('data-testid="router-outlet"');

    expect(focusAt).toBeGreaterThanOrEqual(0);
    expect(outletAt).toBeGreaterThanOrEqual(0);
    expect(focusAt).toBeLessThan(outletAt);
  });
});
