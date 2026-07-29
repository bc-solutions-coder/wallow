import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { ProfileSection } from "./ProfileSection";

/**
 * Component spec for the read-only settings profile section (Wallow-8w1h.6.2).
 *
 * Data flows through the GENERATED `usersGetCurrentUserOptions`, bound to the
 * SDK instance the section reads off the router context. `renderWithWallow`
 * supplies that instance over the harness transport (Wallow-pu6a.5.5), so each
 * profile state is driven by the RESPONSE the section's own request gets rather
 * than by pre-seeding a cache key, and the loading state by a never-settling
 * request (`harness.pending()`).
 *
 * Profile is READ-ONLY (scout CRITICAL DIVERGENCE #1): data comes from
 * `usersGetCurrentUser` ->
 * `CurrentUserResponse{ id, email, firstName, lastName, roles, permissions }`,
 * and there is NO edit/save affordance — the profile is display-only, rendered
 * from name/email/roles off the authenticated principal with no mutation. Testids mirror the
 * C# page object `SettingsProfileSection`:
 *   settings-profile-name, settings-profile-email,
 *   settings-profile-roles (container) + settings-profile-role (per role) OR
 *   settings-profile-no-roles (mutually exclusive), plus a loading state.
 */

const profile = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner", "Admin"],
  permissions: [],
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("ProfileSection", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the profile name and email from the seeded query data", async () => {
    harness.resolveJson(profile);

    renderWithWallow(<ProfileSection />, { harness });

    await expect
      .element(page.getByTestId("settings-profile-name"))
      .toHaveTextContent("Ada Lovelace");
    await expect
      .element(page.getByTestId("settings-profile-email"))
      .toHaveTextContent("ada@lovelace.io");
  });

  it("renders one role element per role inside the roles container", async () => {
    harness.resolveJson(profile);

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-roles")).toBeInTheDocument();
    const roleEls = page.getByTestId("settings-profile-role").elements();
    expect(roleEls).toHaveLength(2);
    expect(roleEls[0]).toHaveTextContent("Owner");
    expect(roleEls[1]).toHaveTextContent("Admin");
    await expect.element(page.getByTestId("settings-profile-no-roles")).not.toBeInTheDocument();
  });

  it("renders the no-roles state when the profile has no roles", async () => {
    harness.resolveJson({ ...profile, roles: [] });

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-no-roles")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-profile-roles")).not.toBeInTheDocument();
  });

  it("renders 'Not set' when name and email are missing", async () => {
    harness.resolveJson({
      id: "u2",
      email: null,
      firstName: null,
      lastName: null,
      roles: [],
      permissions: [],
    });

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-name")).toHaveTextContent("Not set");
    await expect.element(page.getByTestId("settings-profile-email")).toHaveTextContent("Not set");
  });

  it("renders the loading state while the profile query is pending", async () => {
    // Never-settling request keeps the query in the pending state.
    harness.pending();

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-loading")).toBeInTheDocument();
  });
});
