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

import { Route as errorRoute } from "@app/routes/error";
import { ErrorPage } from "./ErrorPage";

/**
 * Error screen: the `reason` query parameter, and the copy each one maps to.
 *
 * This screen is a contract, not a leaf. Every other screen's open-redirect
 * refusal lands here via `/error?reason=invalid_redirect_uri`, and the OIDC
 * flows route here with a membership refusal or with `access_denied` /
 * `invalid_request`. Every one of those reasons has a caller elsewhere, so the
 * mapping is pinned exhaustively rather than by sampling.
 */

/** The refusals that mean the signed-in person is the wrong person. */
const MEMBERSHIP_REASONS = [
  "not_a_member",
  "access_requested",
  "membership_suspended",
  "membership_denied",
] as const;

/** Every reason the app routes here with. */
const REASONS: readonly { readonly reason: string; readonly matches: RegExp }[] = [
  { reason: "not_a_member", matches: /don't have access to this application/iu },
  { reason: "access_requested", matches: /waiting for an administrator to review/iu },
  { reason: "membership_suspended", matches: /has been suspended/iu },
  { reason: "membership_denied", matches: /was not approved/iu },
  { reason: "email_unverified", matches: /verify your email address/iu },
  { reason: "invalid_redirect_uri", matches: /redirect destination is not permitted/iu },
  { reason: "access_denied", matches: /access was denied/iu },
  { reason: "invalid_request", matches: /request was invalid/iu },
];

describe("ErrorPage", () => {
  it("says something went wrong", async () => {
    render(<ErrorPage reason="access_denied" />);

    await expect
      .element(page.getByTestId("error-heading"))
      .toHaveTextContent(/something went wrong/iu);
  });

  it.each(REASONS)("explains the $reason failure", async ({ reason, matches }) => {
    render(<ErrorPage reason={reason} />);

    await expect.element(page.getByTestId("error-message")).toHaveTextContent(matches);
  });

  it("falls back to a generic message for an unrecognised reason", async () => {
    // A reason this page has never heard of must still produce a page — this is
    // the error screen; it has nowhere to escalate to.
    render(<ErrorPage reason="wat" />);

    await expect
      .element(page.getByTestId("error-message"))
      .toHaveTextContent(/unexpected error occurred/iu);
  });

  it("falls back to a generic message when there is no reason at all", async () => {
    render(<ErrorPage />);

    await expect
      .element(page.getByTestId("error-message"))
      .toHaveTextContent(/unexpected error occurred/iu);
  });

  it("never echoes the raw reason into the page", async () => {
    // The reason is a routing key, not copy. Echoing it would put attacker-
    // controlled query-string text on screen — `/error?reason=<anything>` is a
    // URL anyone can construct and send to a victim.
    render(<ErrorPage reason="you-have-been-hacked-call-555-1234" />);

    await expect.element(page.getByTestId("error-heading")).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/555-1234/u);
  });

  it("offers a way home", async () => {
    render(<ErrorPage reason="access_denied" />);

    await expect.element(page.getByTestId("error-back-link")).toHaveAttribute("href", "/");
  });
});

describe("ErrorPage — the membership escape hatch", () => {
  it.each(MEMBERSHIP_REASONS)(
    "offers %s a way to sign out and try another account",
    async (reason) => {
      // A membership refusal means "you are signed in, as the wrong person" — the
      // only case where the fix is to sign out, and the one case where a
      // back-to-home link alone would loop the user into the same error.
      render(<ErrorPage reason={reason} />);

      await expect
        .element(page.getByTestId("error-sign-out-link"))
        .toHaveAttribute("href", "/logout");
    },
  );

  it.each(["invalid_redirect_uri", "access_denied", "invalid_request", "email_unverified", "wat"])(
    "withholds the sign-out link for %s",
    async (reason) => {
      // The gate is the point: signing the user out of a working session because
      // a redirect_uri was malformed would be a hostile non-sequitur, and an
      // unverified address is fixed from the inbox, not by becoming somebody else.
      render(<ErrorPage reason={reason} />);

      await expect.element(page.getByTestId("error-message")).toBeInTheDocument();
      expect(page.getByTestId("error-sign-out-link").query()).toBeNull();
    },
  );

  it("withholds the sign-out link when there is no reason", async () => {
    render(<ErrorPage />);

    await expect.element(page.getByTestId("error-message")).toBeInTheDocument();
    expect(page.getByTestId("error-sign-out-link").query()).toBeNull();
  });

  it("still offers a way home alongside the sign-out link", async () => {
    render(<ErrorPage reason="not_a_member" />);

    await expect.element(page.getByTestId("error-back-link")).toBeInTheDocument();
    await expect.element(page.getByTestId("error-sign-out-link")).toBeInTheDocument();
  });
});

/**
 * Render the route through a real memory router rather than by poking at
 * `Route.options.component`: the component reads `reason` through
 * `Route.useSearch()`, and every router hook dereferences a router that is
 * `null` outside a `RouterProvider` (`useRouter` only warns; `useMatch` then
 * throws on `router.stores`). The root route here is a throwaway — the app's
 * real `__root.tsx` renders `<html>`.
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    errorRoute.update({ id: "/error", path: "/error", getParentRoute: () => rootRoute } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return render(<RouterProvider router={router} />);
}

describe("/error route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    renderRouteAt("/error?reason=not_a_member");

    await expect.element(page.getByTestId("error-heading")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // `not_a_member` earns a sign-out link, so asserting it here proves the
    // query string threaded through `validateSearch` into the screen. A route
    // that dropped `reason` would render the generic message and still pass a
    // bare-render check.
    await expect.element(page.getByTestId("error-sign-out-link")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("error-message"))
      .toHaveTextContent(/don't have access to this application/iu);
  });

  it("reads reason off the query string", () => {
    const validateSearch = errorRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(validateSearch?.({ reason: "not_a_member" })).toEqual({ reason: "not_a_member" });
    expect(validateSearch?.({})).toEqual({ reason: undefined });
  });
});
