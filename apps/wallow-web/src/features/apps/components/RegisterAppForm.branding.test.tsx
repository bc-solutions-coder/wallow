/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

// No global `expect` (vitest `globals` is off) — register jest-dom matchers.
expect.extend(matchers);

/**
 * App branding/logo-upsert reachability spec (Wallow-ffpq.3.6) — the React port
 * of the optional "Branding" section on Blazor `RegisterApp.razor` (which calls
 * `AppService.UpsertBrandingAsync` with a display name, tagline, and logo file).
 * The branding section lives on the same register-app page, so once
 * `RegisterAppForm` is mounted (at `/dashboard/apps/register`, Wallow-ffpq.3.5)
 * the branding display-name / tagline / logo inputs must be reachable in the
 * form view. Testids follow the component's own `app-*` convention (the React
 * port already renamed the Blazor `register-app-*` testids to `app-*`).
 *
 * The `getWallowSdk()` facade is mocked as in `RegisterAppForm.test.tsx`.
 */

const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  register: vi.fn(),
  upsertBranding: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    apps: {
      list: mocks.list,
      get: mocks.get,
      register: mocks.register,
      upsertBranding: mocks.upsertBranding,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("RegisterAppForm branding/logo upsert", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders an optional branding display-name input in the form view", () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    expect(screen.getByTestId("app-branding-display-name")).toBeInTheDocument();
  });

  it("renders an optional branding tagline input", () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    expect(screen.getByTestId("app-branding-tagline")).toBeInTheDocument();
  });

  it("renders a logo file input for the branding upsert", () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    expect(screen.getByTestId("app-logo-input")).toBeInTheDocument();
  });
});
