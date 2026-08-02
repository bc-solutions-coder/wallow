import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { getRouter } from "@app/router";
import { Route } from "./invitations";

/**
 * The dashboard outstanding-invitations route: page root, title, prefetch
 * loader, and router registration.
 *
 * The page mounts `InvitationList`, whose `useQuery` runs for real against the
 * harness transport — nothing in the path is stubbed.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("routes/dashboard/organizations/invitations (route page)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the invitations list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-invitations", async () => {
    harness.resolveJson([]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("dashboard-invitations")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("invitations-heading-title"))
      .toHaveTextContent("Outstanding invitations");
  });

  it("mounts InvitationList (invitations-table)", async () => {
    harness.resolveJson([
      {
        id: "i1",
        email: "ada@acme.io",
        status: "Pending",
        expiresAt: "2026-09-01T12:00:00Z",
        createdAt: "2026-08-01T00:00:00Z",
        acceptedByUserId: null,
      },
    ]);

    const Page = Route.options.component!;
    renderWithWallow(<Page />, { harness });

    await expect.element(page.getByTestId("invitations-table")).toBeInTheDocument();
  });
});

describe("routes/dashboard/organizations/invitations (router registration)", () => {
  it("registers /dashboard/organizations/invitations in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/dashboard/organizations/invitations");
  });
});
