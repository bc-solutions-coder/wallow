import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * The MFA settings card's status-query error state.
 *
 * `enabled = status?.enabled ?? false` makes a failed read look identical to a
 * confirmed "MFA is off", inviting the user to enrol an account that may already
 * be enrolled. The read error carries its OWN testid rather than the mutations'
 * `settings-mfa-error`, which the E2E page object binds to the confirm panel.
 */

/** An RFC 7807 body the SDK's error interceptor parses into an `ApiFailure`. */
const PROBLEM = {
  status: 500,
  code: "Server.Error",
  title: "Internal Server Error",
  detail: "MFA status unavailable.",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("MfaSettingsSection — status query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the status query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<MfaSettingsSection />, { harness });

    await expect
      .element(page.getByTestId("settings-mfa-status-error"))
      .toHaveTextContent("MFA status unavailable.");
  });

  it("does not claim MFA is disabled when the status query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<MfaSettingsSection />, { harness });

    await expect.element(page.getByTestId("settings-mfa-status-error")).toBeInTheDocument();
    expect(page.getByTestId("settings-mfa-status").elements()).toHaveLength(0);
    expect(page.getByTestId("settings-mfa-enable").elements()).toHaveLength(0);
  });
});
