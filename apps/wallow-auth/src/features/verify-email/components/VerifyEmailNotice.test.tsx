import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import { Route as verifyEmailRoute } from "@app/routes/verify-email/index";
import { VerifyEmailNotice } from "./VerifyEmailNotice";

/**
 * Verify-email "check your inbox" screen: inert — no request, no state, one
 * computed href — so there is no harness and nothing to fake.
 *
 * The back-link runs the shipped `isSafeReturnUrl` rather than a stub, because a
 * stub would restate the security rule and become a second copy to get wrong. An
 * unsafe returnUrl is dropped rather than forwarded into the login page's query
 * string, matching the sibling confirmation screen.
 */

describe("VerifyEmailNotice", () => {
  it("tells the user to check their email", async () => {
    await render(<VerifyEmailNotice />);

    await expect
      .element(page.getByTestId("verify-email-heading"))
      .toHaveTextContent(/check your email/iu);
    await expect
      .element(page.getByTestId("verify-email-description"))
      .toHaveTextContent(/sent a verification link/iu);
  });

  it("mentions the spam folder", async () => {
    await render(<VerifyEmailNotice />);

    await expect.element(page.getByTestId("verify-email-heading")).toBeInTheDocument();
    expect(document.body.textContent).toMatch(/spam folder/iu);
  });

  it("links back to sign in", async () => {
    await render(<VerifyEmailNotice />);

    await expect
      .element(page.getByTestId("verify-email-back-link"))
      .toHaveAttribute("href", "/login");
  });

  it("carries a safe returnUrl through to sign in, URL-encoded", async () => {
    await render(<VerifyEmailNotice returnUrl="/apps?a=1&b=2" />);

    await expect
      .element(page.getByTestId("verify-email-back-link"))
      .toHaveAttribute("href", `/login?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`);
  });

  it("drops an unsafe returnUrl from the back link", async () => {
    await render(<VerifyEmailNotice returnUrl="https://evil.example" />);

    await expect
      .element(page.getByTestId("verify-email-back-link"))
      .toHaveAttribute("href", "/login");
  });

  it("drops a protocol-relative returnUrl from the back link", async () => {
    await render(<VerifyEmailNotice returnUrl="//evil.example" />);

    await expect
      .element(page.getByTestId("verify-email-back-link"))
      .toHaveAttribute("href", "/login");
  });
});

/**
 * A real memory router, not `Route.options.component`: the component reads
 * `returnUrl` through `Route.useSearch()`, and a router hook outside a
 * `RouterProvider` dereferences a `null` router (`useRouter` warns, `useMatch`
 * then throws), so a bare render cannot pass.
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    verifyEmailRoute.update({
      id: "/verify-email",
      path: "/verify-email",
      getParentRoute: () => rootRoute,
    } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return render(<RouterProvider router={router} />);
}

describe("/verify-email route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    renderRouteAt(`/verify-email?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`);

    await expect.element(page.getByTestId("verify-email-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // A route that dropped `returnUrl` renders a bare `/login` back-link.
    await expect
      .element(page.getByTestId("verify-email-back-link"))
      .toHaveAttribute("href", `/login?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`);
  });

  it("reads returnUrl off the query string", () => {
    const validateSearch = verifyEmailRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(validateSearch?.({ returnUrl: "/dashboard" })).toEqual({ returnUrl: "/dashboard" });
    expect(validateSearch?.({})).toEqual({ returnUrl: undefined });
  });
});
