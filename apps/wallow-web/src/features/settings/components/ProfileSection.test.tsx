import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ProfileSection } from "./ProfileSection";

/**
 * Component spec for the read-only settings profile section (Wallow-8w1h.6.2).
 *
 * The `getWallowSdk()` facade is mocked so the profile query is inert; the
 * profile state is driven by seeding the `['settings', 'profile']` cache with
 * `setQueryData` (the key that `settingsQueries.profile()` uses), and the
 * loading state by leaving the query to hit a never-resolving facade call.
 *
 * Profile is READ-ONLY (scout CRITICAL DIVERGENCE #1): data comes from
 * `settings.getProfile()` -> `getV1IdentityUsersMe` ->
 * `CurrentUserResponse{ id, email, firstName, lastName, roles, permissions }`,
 * and there is NO edit/save affordance — the Blazor oracle renders name/email/
 * roles off the authenticated principal with no mutation. Testids mirror the
 * C# page object `SettingsProfileSection`:
 *   settings-profile-name, settings-profile-email,
 *   settings-profile-roles (container) + settings-profile-role (per role) OR
 *   settings-profile-no-roles (mutually exclusive), plus a loading state.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  getProfile: vi.fn(),
}));

// Mock the facade module the feature's api.ts imports (`../../lib/wallow-sdk`
// from features/settings; `../../../lib/wallow-sdk` from here).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    settings: { getProfile: mocks.getProfile },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const profile = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner", "Admin"],
  permissions: [],
};

describe("ProfileSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the profile name and email from the seeded query data", async () => {
    const client = newClient();
    client.setQueryData(["settings", "profile"], profile);

    renderWithClient(client, <ProfileSection />);

    await expect
      .element(page.getByTestId("settings-profile-name"))
      .toHaveTextContent("Ada Lovelace");
    await expect
      .element(page.getByTestId("settings-profile-email"))
      .toHaveTextContent("ada@lovelace.io");
  });

  it("renders one role element per role inside the roles container", async () => {
    const client = newClient();
    client.setQueryData(["settings", "profile"], profile);

    renderWithClient(client, <ProfileSection />);

    await expect.element(page.getByTestId("settings-profile-roles")).toBeInTheDocument();
    const roleEls = page.getByTestId("settings-profile-role").elements();
    expect(roleEls).toHaveLength(2);
    expect(roleEls[0]).toHaveTextContent("Owner");
    expect(roleEls[1]).toHaveTextContent("Admin");
    await expect.element(page.getByTestId("settings-profile-no-roles")).not.toBeInTheDocument();
  });

  it("renders the no-roles state when the profile has no roles", async () => {
    const client = newClient();
    client.setQueryData(["settings", "profile"], { ...profile, roles: [] });

    renderWithClient(client, <ProfileSection />);

    await expect.element(page.getByTestId("settings-profile-no-roles")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-profile-roles")).not.toBeInTheDocument();
  });

  it("renders 'Not set' when name and email are missing", async () => {
    const client = newClient();
    client.setQueryData(["settings", "profile"], {
      id: "u2",
      email: null,
      firstName: null,
      lastName: null,
      roles: [],
      permissions: [],
    });

    renderWithClient(client, <ProfileSection />);

    await expect.element(page.getByTestId("settings-profile-name")).toHaveTextContent("Not set");
    await expect.element(page.getByTestId("settings-profile-email")).toHaveTextContent("Not set");
  });

  it("renders the loading state while the profile query is pending", async () => {
    const client = newClient();
    // Never-resolving facade call keeps the query in the pending state.
    mocks.getProfile.mockReturnValue(new Promise(() => {}));

    renderWithClient(client, <ProfileSection />);

    await expect.element(page.getByTestId("settings-profile-loading")).toBeInTheDocument();
  });
});
