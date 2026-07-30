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
 * Component spec for the VerifyEmail "check your inbox" screen (Wallow-vec7.3.3).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `verify-email-heading`, `verify-email-description`, `verify-email-back-link`.
 *
 * This screen is inert — no SDK call, no state, one computed href. It exists to
 * tell the user to go read their email.
 *
 * ── A DELIBERATE DEVIATION FROM THE ORACLE (hardening) ───────────────────────
 *
 * The oracle computes its back-link as:
 *
 *     private string LoginUrl => string.IsNullOrEmpty(ReturnUrl)
 *         ? "/login"
 *         : $"/login?returnUrl={Uri.EscapeDataString(ReturnUrl)}";
 *
 * — `IsNullOrEmpty`, NOT `ReturnUrlValidator.IsSafe`. Its sibling
 * `VerifyEmailConfirm.razor` guards the very same link with `IsSafe`, so this is
 * an inconsistency in the original rather than a decision: this page will
 * forward `?returnUrl=https://evil.example` into the login page's query string
 * untouched.
 *
 * The port applies the guard here too (an unsafe returnUrl is dropped, exactly
 * as VerifyEmailConfirm drops it). This is a deviation from the oracle and is
 * called out as such — it strictly narrows behaviour, forwards nothing hostile,
 * and makes the two sibling screens agree. It is NOT the refuse-vs-sanitize case
 * from bd memory `returnurl-guard-refuse-dont-sanitize`: nothing navigates here,
 * the screen merely declines to hand a hostile value to the next screen. Flagged
 * on the bead for the verifier.
 *
 * ── TEST SEAM: NONE, AND THAT IS THE POINT (Wallow-pu6a.5.1) ─────────────────
 *
 * This file used to `vi.mock` the app's SDK facade purely to hand the screen an
 * `isSafeReturnUrl` stub — and then restate the real rule inside that stub,
 * because a stub answering a flat `true`/`false` would have proved nothing about
 * the guard. A restatement of a security rule is a second copy to get wrong, so
 * the mock is gone and `signInHref` reaches the REAL
 * `isSafeReturnUrl` (`packages/sdk/src/auth-oidc.ts`) — a pure string-in/
 * boolean-out function with no transport underneath it.
 *
 * No `sdk-harness` here either: the screen is inert, issues no request, and has
 * nothing to fake. The cases below (`/apps?a=1&b=2` safe, `https://evil.example`
 * and `//evil.example` unsafe) are now answered by the shipped guard rather than
 * by this file's own opinion of it.
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
    // The oracle's card body — the single most useful sentence on the page, and
    // trivially easy to drop when porting the "static" parts by eye.
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
    // The deliberate deviation — see this file's header.
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
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`: this route's component reads `returnUrl` through
 * `Route.useSearch()`, and every router hook dereferences a `null` router
 * outside a `RouterProvider` (`useRouter` only warns; `useMatch` then throws on
 * `router.stores`), so a bare render is unsatisfiable by any correct
 * implementation. Mirrors `ResetPasswordForm.test.tsx`'s harness.
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
    // Proves the query string threaded through `validateSearch` into the screen:
    // a route that dropped `returnUrl` would render a bare `/login` back-link.
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
