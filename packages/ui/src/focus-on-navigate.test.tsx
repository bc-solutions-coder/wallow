import { render } from "@bc-solutions-coder/testing/render";
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
  type AnyRouter,
} from "@tanstack/react-router";
import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";

import { FocusOnNavigate, MAIN_HEADING_SELECTOR } from "./focus-on-navigate";

/**
 * The moved spec for the shared FocusOnNavigate (Wallow-0q2s.6.3). Ported
 * verbatim from apps/wallow-auth/src/components/focus-on-navigate.test.tsx — the
 * component was auth-only today and moves into @bc-solutions-coder/ui unchanged.
 *
 * The React port of Blazor's `<FocusOnNavigate Selector="h1" />`
 * (Wallow-ffpq.2.8). Blazor moved focus to the page's `<h1>` on every route
 * change so screen readers announce the new screen; the port must do the same.
 *
 * Every case drives the REAL TanStack router through a memory history — the
 * behaviour under test only means anything across an actual client-side
 * navigation: mount `FocusOnNavigate` at the root next to the `<Outlet/>`, then
 * navigate and watch where focus lands. Each route renders a single `<h1>`,
 * exactly as `auth-layout.tsx` does on every screen.
 */
function buildRouter(initialPath: string): AnyRouter {
  const rootRoute = createRootRoute({
    component: () => (
      <>
        <FocusOnNavigate />
        <Outlet />
      </>
    ),
  });

  const loginRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/login",
    component: () => <h1 data-testid="login-heading">Sign in</h1>,
  });

  const registerRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/register",
    component: () => <h1 data-testid="register-heading">Create account</h1>,
  });

  const routeTree = rootRoute.addChildren([loginRoute, registerRoute]);

  return createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });
}

async function renderAt(initialPath: string, initialTestId: string): Promise<AnyRouter> {
  const router = buildRouter(initialPath);
  render(<RouterProvider router={router} />);
  // Wait for the initial route to resolve and paint before we drive navigation.
  await expect.element(page.getByTestId(initialTestId)).toBeInTheDocument();
  return router;
}

async function navigateTo(router: AnyRouter, path: string): Promise<void> {
  await router.navigate({ to: path });
}

describe("FocusOnNavigate", () => {
  it("pins the main-heading selector to Blazor's `h1`", () => {
    // The Blazor oracle is `<FocusOnNavigate Selector="h1" />`; the branding
    // `<h1>` in auth-layout.tsx is the announced landmark on every screen.
    expect(MAIN_HEADING_SELECTOR).toBe("h1");
  });

  it("moves focus to the destination heading on a client-side navigation", async () => {
    const router = await renderAt("/login", "login-heading");

    await navigateTo(router, "/register");

    await expect.element(page.getByTestId("register-heading")).toHaveFocus();
  });

  it("makes the heading programmatically focusable without adding it to the tab order", async () => {
    // A bare `<h1>` is not focusable, so `.focus()` is a no-op until the heading
    // is given `tabindex="-1"` — which also keeps it OUT of the Tab sequence, so
    // sighted keyboard users are not forced to tab through the heading. This is
    // the standard SPA route-change focus pattern and what makes the focus above
    // actually land.
    const router = await renderAt("/login", "login-heading");

    await navigateTo(router, "/register");

    await expect.element(page.getByTestId("register-heading")).toHaveFocus();
    await expect.element(page.getByTestId("register-heading")).toHaveAttribute("tabindex", "-1");
  });

  it("re-targets the new heading on each navigation, not a stale one", async () => {
    const router = await renderAt("/login", "login-heading");

    await navigateTo(router, "/register");
    await expect.element(page.getByTestId("register-heading")).toHaveFocus();

    await navigateTo(router, "/login");
    await expect.element(page.getByTestId("login-heading")).toHaveFocus();
  });

  it("does not steal focus on the initial page load", async () => {
    // The accessibility win is about CLIENT-SIDE navigation, where no full page
    // load announces the change. On the first render the browser has just placed
    // focus for a real page load, so the component must leave it alone — focus
    // stays on the body rather than being yanked to the heading.
    await renderAt("/login", "login-heading");

    await expect.element(page.getByTestId("login-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-heading")).not.toHaveFocus();
    expect(document.activeElement).toBe(document.body);
  });
});
