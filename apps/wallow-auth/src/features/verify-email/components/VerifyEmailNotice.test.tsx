/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as verifyEmailRoute } from "../../../routes/verify-email/index";
import { VerifyEmailNotice } from "./VerifyEmailNotice";

expect.extend(matchers);

/**
 * Component spec for the VerifyEmail "check your inbox" screen (Wallow-vec7.3.3),
 * ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/VerifyEmail.razor`.
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
 */

const mocks = vi.hoisted(() => ({
  isSafeReturnUrl: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {},
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
}));

/** The real `isSafeReturnUrl` rule — see the note in VerifyEmailConfirm.test.tsx. */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
});

describe("VerifyEmailNotice", () => {
  it("tells the user to check their email", () => {
    render(<VerifyEmailNotice />);

    expect(screen.getByTestId("verify-email-heading")).toHaveTextContent(/check your email/iu);
    expect(screen.getByTestId("verify-email-description")).toHaveTextContent(
      /sent a verification link/iu,
    );
  });

  it("mentions the spam folder", () => {
    // The oracle's card body — the single most useful sentence on the page, and
    // trivially easy to drop when porting the "static" parts by eye.
    render(<VerifyEmailNotice />);

    expect(document.body.textContent).toMatch(/spam folder/iu);
  });

  it("links back to sign in", () => {
    render(<VerifyEmailNotice />);

    expect(screen.getByTestId("verify-email-back-link")).toHaveAttribute("href", "/login");
  });

  it("carries a safe returnUrl through to sign in, URL-encoded", () => {
    render(<VerifyEmailNotice returnUrl="/apps?a=1&b=2" />);

    expect(screen.getByTestId("verify-email-back-link")).toHaveAttribute(
      "href",
      `/login?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`,
    );
  });

  it("drops an unsafe returnUrl from the back link", () => {
    // The deliberate deviation — see this file's header.
    render(<VerifyEmailNotice returnUrl="https://evil.example" />);

    expect(screen.getByTestId("verify-email-back-link")).toHaveAttribute("href", "/login");
  });

  it("drops a protocol-relative returnUrl from the back link", () => {
    render(<VerifyEmailNotice returnUrl="//evil.example" />);

    expect(screen.getByTestId("verify-email-back-link")).toHaveAttribute("href", "/login");
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
    }),
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

    expect(await screen.findByTestId("verify-email-heading")).toBeInTheDocument();
    expect(screen.queryByTestId("route-placeholder")).toBeNull();
    // Proves the query string threaded through `validateSearch` into the screen:
    // a route that dropped `returnUrl` would render a bare `/login` back-link.
    expect(screen.getByTestId("verify-email-back-link")).toHaveAttribute(
      "href",
      `/login?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`,
    );
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
