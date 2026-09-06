import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { ProfileSection } from "./ProfileSection";

/**
 * ProfileSection's query error state.
 *
 * The card collapses `data ?? {}`, so without its own error branch a failed
 * `usersGetCurrentUserOptions()` read paints a fully-populated-looking card
 * whose every value is the "Not set" fallback — presenting an error as a fact
 * about the account.
 */

/** An RFC 7807 body the SDK's error interceptor parses into an `ApiFailure`. */
const PROBLEM = {
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  detail: "Profile is unavailable.",
};

let harness: SdkHarness;

describe("ProfileSection — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the server-failure copy, not the 500's detail, when the current-user query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<ProfileSection />, { harness });

    await expect
      .element(page.getByTestId("settings-profile-error"))
      .toHaveTextContent("Something went wrong on our side. Please try again later.");
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
