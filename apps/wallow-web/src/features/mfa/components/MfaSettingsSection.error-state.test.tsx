import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * Status-query error-state spec for the MFA settings card (Wallow-lrlm.4.2).
 *
 * The card's two MUTATIONS already surface failures through `settings-mfa-error`
 * — it is the initial `mfaGetStatusOptions()` READ that has no error branch.
 * `enabled = status?.enabled ?? false` makes a failed read look identical to a
 * confirmed "MFA is off", which is the most dangerous instance of this bug in
 * the app: it invites the user to enrol an account that may already be enrolled.
 *
 * The query error gets its OWN testid, `settings-mfa-status-error`, rather than
 * reusing the mutations' `settings-mfa-error`. The two never co-render (the read
 * error returns before the card exists), but a spec asserting on one testid must
 * be able to say WHICH failure it saw, and the E2E page object already binds
 * `settings-mfa-error` to the confirm-panel flow.
 */

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = { status: 500, title: "Internal Server Error", detail: "MFA status unavailable." };

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
