import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../../router";
import { Route } from "./settings";

/**
 * Route spec for the dashboard settings route (Wallow-8w1h.6.5), mirroring
 * routes/dashboard/apps/index.test.tsx. Covers three contracts:
 *   1. The route page renders a root carrying `data-testid="dashboard-settings"`
 *      and prefetches both queries via a `loader`.
 *   2. Both composed sections render inside that root — the profile section
 *      (`settings-profile-*`) and the MFA status card (`settings-mfa-*`).
 *   3. `src/router.tsx` registers the route under the root at
 *      `/dashboard/settings` (bound manually, no dashboard layout route yet).
 */

// The rendered page mounts ProfileSection (settings.getProfile) and
// MfaSettingsSection (mfa.status + disable/regenerate mutations); mock the
// facade so both queries are inert during the route render.
const mocks = vi.hoisted(() => ({
  getProfile: vi.fn(),
  status: vi.fn(),
  enrollTotp: vi.fn(),
  confirmEnroll: vi.fn(),
  disable: vi.fn(),
  regenerateBackupCodes: vi.fn(),
}));

vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    settings: { getProfile: mocks.getProfile },
    mfa: {
      status: mocks.status,
      enrollTotp: mocks.enrollTotp,
      confirmEnroll: mocks.confirmEnroll,
      disable: mocks.disable,
      regenerateBackupCodes: mocks.regenerateBackupCodes,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function seededClient(): QueryClient {
  const client = newClient();
  // Seed both section caches so the composed sections render content (not
  // their loading states) during the route render.
  client.setQueryData(["settings", "profile"], {
    id: "user-1",
    email: "ada@lovelace.io",
    firstName: "Ada",
    lastName: "Lovelace",
    roles: ["Owner"],
    permissions: [],
  });
  client.setQueryData(["mfa", "status"], {
    enabled: false,
    method: null,
    backupCodeCount: 0,
  });
  return client;
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("routes/dashboard/settings (route page)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("prefetches profile + mfa status via a loader", () => {
    expect(Route.options.loader).toBeDefined();
  });

  it("renders a page root carrying data-testid=dashboard-settings", async () => {
    const Page = Route.options.component!;
    renderWithClient(seededClient(), <Page />);

    await expect.element(page.getByTestId("dashboard-settings")).toBeInTheDocument();
  });

  it("composes the profile section inside the dashboard-settings root", async () => {
    const Page = Route.options.component!;
    renderWithClient(seededClient(), <Page />);

    const root = page.getByTestId("dashboard-settings");
    await expect
      .element(root.getByTestId("settings-profile-name"))
      .toHaveTextContent("Ada Lovelace");
    await expect
      .element(root.getByTestId("settings-profile-email"))
      .toHaveTextContent("ada@lovelace.io");
  });

  it("composes the mfa status card inside the dashboard-settings root", async () => {
    const Page = Route.options.component!;
    renderWithClient(seededClient(), <Page />);

    const root = page.getByTestId("dashboard-settings");
    await expect.element(root.getByTestId("settings-mfa-status")).toHaveTextContent("Disabled");
  });
});

describe("routes/dashboard/settings (router registration)", () => {
  it("registers /dashboard/settings in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/dashboard/settings");
  });
});
