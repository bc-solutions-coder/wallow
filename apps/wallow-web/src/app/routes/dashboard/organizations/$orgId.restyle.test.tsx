import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, it, vi } from "vitest";

import {
  expectPageContainer,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";

/**
 * The organization-detail route shell: the column width the page sits in. The
 * page body is `OrganizationDetail`, which has its own restyle spec.
 *
 * The page reads `Route.useParams()`, which throws outside a `RouterProvider`,
 * so `createFileRoute` is stubbed to hand back a plain route object with a fixed
 * `orgId`.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    createFileRoute: () => (options: Record<string, unknown>) => ({
      options,
      useParams: () => ({ orgId: "o1" }),
    }),
  };
});

const ORG = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const MEMBERS = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["Owner"],
  },
];

const CLIENTS = [{ id: "c1", clientId: "acme-web", name: "Acme Web" }];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const { Route } = await import("./$orgId");
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  return waitForTestId("dashboard-organization-detail");
}

describe("routes/dashboard/organizations/$orgId (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    // Any unrouted request (a refetch after a lifecycle mutation) resolves empty
    // so no list render ever sees a non-array body.
    routeHarness(
      harness,
      {
        "GET /v1/identity/organizations/o1": ORG,
        "GET /v1/identity/organizations/o1/members": MEMBERS,
        "GET /v1/identity/clients/by-tenant/o1": CLIENTS,
      },
      { fallback: [] },
    );
  });

  it("centers the detail page in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
