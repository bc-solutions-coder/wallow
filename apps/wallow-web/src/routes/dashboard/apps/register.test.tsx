/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../../router";
import { Route } from "./register";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly (same convention as the component tests).
expect.extend(matchers);

/**
 * Route spec for the register-app route (Wallow-ffpq.3.5) — the intended mount
 * point for the orphan `RegisterAppForm`. Mirrors
 * routes/dashboard/apps/index.test.tsx. Covers three contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-apps-
 *      register"` and mounts `RegisterAppForm` (its `app-register-form` testid).
 *   2. `src/router.tsx` registers the route under `/dashboard` at
 *      `/dashboard/apps/register` (bound manually alongside `apps`, no
 *      file-based codegen yet).
 *
 * RED note (list-route gotcha, Wallow-8w1h.5.2): "exposes a route component"
 * passes on the compile-safe stub because `createFileRoute` always defines a
 * component; the wrapper-render, form-mount, and router-registration assertions
 * fail until GREEN.
 */

// RegisterAppForm builds its mutation from `registerAppMutation(queryClient)`,
// which reaches the `apps.register` facade slice; mock it so the form is inert
// during the route render (mirrors apps/index.test.tsx's facade mock).
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

describe("routes/dashboard/apps/register (route page)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-apps-register", () => {
    const client = newClient();

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    expect(screen.getByTestId("dashboard-apps-register")).toBeInTheDocument();
  });

  it("mounts the RegisterAppForm (app-register-form)", () => {
    const client = newClient();

    const Page = Route.options.component!;
    renderWithClient(client, <Page />);

    expect(screen.getByTestId("app-register-form")).toBeInTheDocument();
  });
});

describe("routes/dashboard/apps/register (router registration)", () => {
  it("registers /dashboard/apps/register in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/apps/register");
  });
});
