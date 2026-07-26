import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { ProfileSection } from "./ProfileSection";

/**
 * Component spec for the read-only settings profile section (Wallow-8w1h.6.2).
 *
 * Data flows through the SDK query layer (`settingsQueries.profile()`), so the
 * network seam is the shared SDK client's `fetch`, overridden per test via
 * `installSdkClientMock` (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade
 * is no longer in the path). The profile state is driven by seeding the
 * `['settings', 'profile']` cache with `setQueryData` (the key that
 * `settingsQueries.profile()` uses; `staleTime: Infinity` keeps the seeded data
 * from refetching), and the loading state by leaving the query to hit a
 * never-settling request (`sdk.pending()`).
 *
 * Profile is READ-ONLY (scout CRITICAL DIVERGENCE #1): data comes from
 * `settingsQueries.profile()` -> `getV1IdentityUsersMe` ->
 * `CurrentUserResponse{ id, email, firstName, lastName, roles, permissions }`,
 * and there is NO edit/save affordance — the profile is display-only, rendered
 * from name/email/roles off the authenticated principal with no mutation. Testids mirror the
 * C# page object `SettingsProfileSection`:
 *   settings-profile-name, settings-profile-email,
 *   settings-profile-roles (container) + settings-profile-role (per role) OR
 *   settings-profile-no-roles (mutually exclusive), plus a loading state.
 */

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
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
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
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
    // Never-settling request keeps the query in the pending state.
    sdk.pending();

    renderWithClient(client, <ProfileSection />);

    await expect.element(page.getByTestId("settings-profile-loading")).toBeInTheDocument();
  });
});
