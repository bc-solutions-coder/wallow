import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { ProfileSection } from "./ProfileSection";

/**
 * ProfileSection — the settings profile card. It is READ-ONLY: name, email and
 * roles come off the authenticated principal, with no edit or save affordance.
 *
 * Data flows through the GENERATED `usersGetCurrentUserOptions`, bound to the
 * SDK instance the section reads off the router context. `renderWithWallow`
 * supplies that instance over the harness transport, so each state is driven by
 * the RESPONSE the section's own request gets rather than by a pre-seeded cache
 * key — and the loading state by a never-settling request (`harness.pending()`).
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

/** The caption above a field's value element, which sits in its parent row. */
function captionOf(testId: string): string {
  const row = page.getByTestId(testId).element().parentElement;
  return row?.querySelector("span")?.textContent ?? "";
}

describe("ProfileSection", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the profile name and email from the seeded query data", async () => {
    harness.resolveJson(profile);

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByRole("heading", { name: "Profile" })).toBeInTheDocument();
    await expect
      .element(page.getByTestId("settings-profile-name"))
      .toHaveTextContent("Ada Lovelace");
    await expect
      .element(page.getByTestId("settings-profile-email"))
      .toHaveTextContent("ada@lovelace.io");
    expect(captionOf("settings-profile-name")).toBe("Name");
    expect(captionOf("settings-profile-email")).toBe("Email");
  });

  it("renders one role element per role inside the roles container", async () => {
    harness.resolveJson(profile);

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-roles")).toBeInTheDocument();
    expect(captionOf("settings-profile-roles")).toBe("Roles");
    const roleEls = page.getByTestId("settings-profile-role").elements();
    expect(roleEls).toHaveLength(2);
    expect(roleEls[0]).toHaveTextContent("Owner");
    expect(roleEls[1]).toHaveTextContent("Admin");
    await expect.element(page.getByTestId("settings-profile-no-roles")).not.toBeInTheDocument();
  });

  it("renders the no-roles state when the profile has no roles", async () => {
    harness.resolveJson({ ...profile, roles: [] });

    renderWithWallow(<ProfileSection />, { harness });

    await expect
      .element(page.getByTestId("settings-profile-no-roles"))
      .toHaveTextContent("No roles assigned.");
    expect(captionOf("settings-profile-no-roles")).toBe("Roles");
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

    await expect
      .element(page.getByTestId("settings-profile-loading"))
      .toHaveTextContent("Loading profile…");
  });
});
