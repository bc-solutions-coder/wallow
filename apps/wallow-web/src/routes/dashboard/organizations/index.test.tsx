import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../../router";
import { Route } from "./index";

/**
 * Route spec for the CANONICAL dashboard list route (Wallow-8w1h.4.2). Covers
 * two contracts every later vertical (Phases 4-6) copies:
 *   1. The route page renders a root carrying `data-testid="dashboard-
 *      organizations"` and prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at
 *      `/dashboard/organizations` (no dashboard layout route exists yet, so the
 *      route is bound manually — see router.tsx's `indexRouteWithParent`).
 */

// The rendered page mounts OrganizationList (useQuery); mock the facade so the
// list query is inert during the route render.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: { list: mocks.list, get: mocks.get, create: mocks.create },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("routes/dashboard/organizations (route page)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the org list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-organizations", async () => {
    const client = newClient();
    client.setQueryData(["orgs"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    await expect.element(page.getByTestId("dashboard-organizations")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the orphan CreateOrganizationForm mounts INLINE on this
  // index page (NOT a separate /dashboard/organizations/create route — no such
  // route exists and the bead's AC forbids recreating it).
  it("mounts the CreateOrganizationForm inline (organization-create-form)", async () => {
    const client = newClient();
    client.setQueryData(["orgs"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    await expect.element(page.getByTestId("organization-create-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/organizations (router registration)", () => {
  it("registers /dashboard/organizations in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/organizations");
  });
});
