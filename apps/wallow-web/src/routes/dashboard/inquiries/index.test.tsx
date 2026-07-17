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
 * Route spec for the dashboard inquiries list route (Wallow-8w1h.7.2), mirroring
 * routes/dashboard/organizations/index.test.tsx. Covers two contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-inquiries"`
 *      and prefetches via a `loader`.
 *   2. `src/router.tsx` registers the route under the root at
 *      `/dashboard/inquiries` (bound manually alongside the other dashboard
 *      routes, no layout route yet).
 *
 * RED note (list-route gotcha, Wallow-8w1h.5.2): "exposes a route component"
 * passes on the compile-safe stub because `createFileRoute` always defines a
 * component; the other three assertions (loader, dashboard-inquiries render,
 * router registration) fail until GREEN.
 */

// The rendered page mounts InquiryList (useQuery); mock the facade so the list
// query is inert during the route render.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  create: vi.fn(),
  get: vi.fn(),
  comments: vi.fn(),
  addComment: vi.fn(),
  setStatus: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    inquiries: {
      list: mocks.list,
      create: mocks.create,
      get: mocks.get,
      comments: mocks.comments,
      addComment: mocks.addComment,
      setStatus: mocks.setStatus,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("routes/dashboard/inquiries (route page)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches the inquiry list via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-inquiries", () => {
    const client = newClient();
    client.setQueryData(["inquiries"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    expect(screen.getByTestId("dashboard-inquiries")).toBeInTheDocument();
  });

  // Wallow-ffpq.3.5 — the orphan CreateInquiryForm mounts INLINE on this index
  // page, matching Blazor's Inquiries.razor (list + create on the SAME page),
  // NOT a standalone route.
  it("mounts the CreateInquiryForm inline (inquiry-create-form)", () => {
    const client = newClient();
    client.setQueryData(["inquiries"], []);

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    expect(screen.getByTestId("inquiry-create-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/inquiries (router registration)", () => {
  it("registers /dashboard/inquiries in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/inquiries");
  });
});
