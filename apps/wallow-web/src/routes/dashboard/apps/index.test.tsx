/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../../router";
import { Route } from "./index";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly (same convention as the component tests).
expect.extend(matchers);

/**
 * Route spec for the dashboard apps list route (Wallow-8w1h.5.2), mirroring
 * routes/dashboard/organizations/index.test.tsx. Covers two contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-apps"` and
 *      prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at `/dashboard/apps`
 *      (bound manually alongside the organizations routes, no layout route yet).
 */

// The rendered page mounts AppList (useQuery); mock the facade so the list query
// is inert during the route render.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  register: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    apps: { list: mocks.list, get: mocks.get, register: mocks.register },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("routes/dashboard/apps (route page)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the app list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-apps", () => {
    const client = newClient();
    client.setQueryData(["apps"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    expect(screen.getByTestId("dashboard-apps")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the apps index links to the register route so
  // RegisterAppForm is reachable via normal UI navigation (mirrors the Blazor
  // oracle's `apps-register-link`), not just a directly-typed URL.
  it("links to the register route (apps-register-link)", () => {
    const client = newClient();
    client.setQueryData(["apps"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    const link = screen.getByTestId("apps-register-link");
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "/dashboard/apps/register");
  });
});

describe("routes/dashboard/apps (router registration)", () => {
  it("registers /dashboard/apps in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/apps");
  });
});
