import { ApiFailure, ClientErrorCode } from "@bc-solutions-coder/api-errors";
import { expectConsoleError } from "@bc-solutions-coder/testing/console-guard";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createRootRoute, createRoute, Outlet } from "@tanstack/react-router";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { RootErrorBoundary } from "./__root";

/**
 * The root boundary's three branches: fixed copy for a render bug, the
 * not-found screen for an API 404, and a `FailureBanner` for every other API
 * failure — plus which shell it paints. Rendered directly it sits at the root
 * match, so `renderWithWallow` supplies the router `useRouter` reads and the
 * banner's retry invalidates; the layout case mounts it as a route's own error
 * component under a throwaway layout, the way `defaultErrorComponent` does.
 */

function noop(): void {
  // The router's `reset`; the boundary never calls it.
}

const SERVER_FAILURE = new ApiFailure({
  status: 503,
  code: ClientErrorCode.TRANSPORT_NETWORK_ERROR,
  title: "Service Unavailable",
  requestId: "req-42",
});

describe("RootErrorBoundary", () => {
  it("renders fixed copy for a render error and never echoes its message", async () => {
    renderWithWallow(<RootErrorBoundary error={new Error("upstream at 10.0.0.1")} reset={noop} />);

    const card = page.getByTestId("root-error");
    await expect.element(card).toHaveTextContent("This page could not be loaded.");
    await expect.element(card).not.toHaveTextContent("10.0.0.1");
    expect(page.getByTestId("root-failure").elements()).toHaveLength(0);
  });

  it("paints the public shell at the root match", async () => {
    renderWithWallow(<RootErrorBoundary error={SERVER_FAILURE} reset={noop} />);

    await expect.element(page.getByTestId("root-failure")).toBeInTheDocument();
    expect(page.getByTestId("public-layout").elements()).toHaveLength(1);
  });

  it("renders inside a layout route's own chrome, not a second shell", async () => {
    const foreignRoot = createRootRoute();
    const shell = createRoute({
      getParentRoute: () => foreignRoot,
      id: "shell",
      component: () => (
        <div data-testid="shell">
          <Outlet />
        </div>
      ),
    });
    const broken = createRoute({
      getParentRoute: () => shell,
      path: "/broken",
      loader: () => {
        throw SERVER_FAILURE;
      },
      errorComponent: RootErrorBoundary,
    });

    renderWithWallow(null, { routes: [shell.addChildren([broken])], path: "/broken" });

    const banner = page.getByTestId("root-failure");
    await expect.element(banner).toHaveTextContent("Reference req-42");
    expect(page.getByTestId("shell").elements()).toHaveLength(1);
    expect(page.getByTestId("public-layout").elements()).toHaveLength(0);
    // The router reports a loader error on the console; the guard fails the
    // test over it unless the spec that provoked it reads it back.
    await expectConsoleError("Transport.NetworkError");
  });

  it("renders the not-found screen for an API 404", async () => {
    const missing = new ApiFailure({ status: 404, code: "Http.NotFound", title: "Not Found" });

    renderWithWallow(<RootErrorBoundary error={missing} reset={noop} />);

    await expect.element(page.getByTestId("root-not-found")).toBeInTheDocument();
    expect(page.getByTestId("root-error").elements()).toHaveLength(0);
  });

  it("renders a failure banner with the reference for any other API failure", async () => {
    renderWithWallow(<RootErrorBoundary error={SERVER_FAILURE} reset={noop} />);

    const banner = page.getByTestId("root-failure");
    await expect
      .element(banner)
      .toHaveTextContent("Unable to reach the server. Check your connection and try again.");
    await expect.element(banner).toHaveTextContent("Reference req-42");
  });

  it("invalidates the router when the banner's retry is clicked", async () => {
    const { router } = renderWithWallow(<RootErrorBoundary error={SERVER_FAILURE} reset={noop} />);
    const invalidate = vi.spyOn(router, "invalidate");

    await userEvent.click(page.getByRole("button", { name: "Try again" }));

    expect(invalidate).toHaveBeenCalledTimes(1);
  });
});
