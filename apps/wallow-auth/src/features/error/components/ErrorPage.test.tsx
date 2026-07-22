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

import { Route as errorRoute } from "../../../routes/error";
import { ErrorPage } from "./ErrorPage";

/**
 * Component spec for the Error screen (Wallow-vec7.3.3).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `error-heading`, `error-message`, `error-sign-out-link`, `error-back-link`.
 *
 * THIS SCREEN IS A CONTRACT, NOT A LEAF. Per bd memory
 * `returnurl-guard-refuse-dont-sanitize`, every other screen's open-redirect
 * refusal lands HERE via `/error?reason=invalid_redirect_uri`, and the OIDC
 * flows route here with `not_a_member` / `access_denied` / `invalid_request`.
 * Each of those reasons is a caller Wallow-vec7.3.x is writing right now, so the
 * `reason` mapping below is pinned exhaustively rather than by sampling.
 *
 * No SDK mock: this screen is inert. It reads one query parameter and renders.
 */

/** The oracle's switch arms, verbatim. */
const REASONS: readonly { readonly reason: string; readonly matches: RegExp }[] = [
  { reason: "not_a_member", matches: /don't have access to this application/iu },
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
    // Oracle's `_` arm. A reason this page has never heard of must still produce
    // a page — this is the error screen; it has nowhere to escalate to.
    render(<ErrorPage reason="wat" />);

    await expect
      .element(page.getByTestId("error-message"))
      .toHaveTextContent(/unexpected error occurred/iu);
  });

  it("falls back to a generic message when there is no reason at all", async () => {
    // `/error` with a bare query string. Same arm — `null` hits `_` in the
    // oracle's switch.
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

describe("ErrorPage — the not_a_member escape hatch", () => {
  it("offers to sign out and try another account", async () => {
    // Oracle: this link is gated on `reason == "not_a_member"`. That case means
    // "you are signed in, as the wrong person" — the ONLY case where the fix is
    // to sign out, and the one case where a back-to-home link alone would loop
    // the user straight back into the same error.
    render(<ErrorPage reason="not_a_member" />);

    await expect
      .element(page.getByTestId("error-sign-out-link"))
      .toHaveAttribute("href", "/logout");
  });

  it.each(["invalid_redirect_uri", "access_denied", "invalid_request", "wat"])(
    "withholds the sign-out link for %s",
    async (reason) => {
      // The gate is the point: signing the user out of a working session because
      // a redirect_uri was malformed would be a hostile non-sequitur.
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
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because this route's component reads `reason`
 * through `Route.useSearch()` — and every router hook dereferences a router that
 * is `null` outside a `RouterProvider` (`useRouter` only warns; `useMatch` then
 * throws on `router.stores`). A bare render is therefore unsatisfiable by any
 * correct implementation, not a bar this screen fails to clear. Mirrors the
 * harness `ResetPasswordForm.test.tsx` established for the same reason.
 *
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    errorRoute.update({ id: "/error", path: "/error", getParentRoute: () => rootRoute }),
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
    // `not_a_member` is the ONE reason that earns a sign-out link, so asserting
    // it here proves the query string actually threaded through
    // `validateSearch` into the screen — strictly more than the bare render
    // pinned. A route that dropped `reason` would render the generic message
    // and fail this line.
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
