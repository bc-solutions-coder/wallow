import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { ProfileSection } from "./ProfileSection";

/**
 * Query error-state spec for the settings profile card (Wallow-lrlm.4.2).
 *
 * `ProfileSection` collapses `data ?? {}`, so a failed
 * `usersGetCurrentUserOptions()` read renders a fully-populated-looking card
 * whose every value is the "Not set" fallback — the failure mode this task
 * exists to end, because it presents an error as a fact about the account.
 */

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = { status: 500, title: "Internal Server Error", detail: "Profile is unavailable." };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("ProfileSection — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the current-user query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<ProfileSection />, { harness });

    await expect
      .element(page.getByTestId("settings-profile-error"))
      .toHaveTextContent("Profile is unavailable.");
  });

  it("does not render the 'Not set' profile fields when the query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<ProfileSection />, { harness });

    await expect.element(page.getByTestId("settings-profile-error")).toBeInTheDocument();
    expect(page.getByTestId("settings-profile-name").elements()).toHaveLength(0);
    expect(page.getByTestId("settings-profile-email").elements()).toHaveLength(0);
    expect(page.getByTestId("settings-profile-no-roles").elements()).toHaveLength(0);
  });
});
