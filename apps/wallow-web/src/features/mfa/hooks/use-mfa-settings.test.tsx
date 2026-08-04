import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { useMfaSettings } from "./use-mfa-settings";

/**
 * The MFA settings state machine, driven through its own returned API rather
 * than through the card.
 *
 * `MfaSettingsSection.test.tsx` covers everything the card can reach by clicking
 * it. What is here is what it cannot: the residue `openConfirm` clears between
 * attempts, and the guard on submitting with no panel open — which the card has
 * no affordance to attempt at all, because it does not render the submit until a
 * panel exists.
 */

const STATUS_PATH = "/api/v1/identity/mfa/status";
const REGENERATE_PATH = "/api/v1/identity/mfa/backup-codes/regenerate";
const ENABLED_STATUS = { enabled: true, method: "totp", backupCodeCount: 7 };

/** What the probe's submit sends. The password itself is the form's business now. */
const PASSWORD = "hunter2";

let harness: SdkHarness;

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body ?? null), {
    status,
    headers: { "content-type": "application/json" },
  });
}

/** Answer the status read with Enabled and every write with `body` at `bodyStatus`. */
function program(body: unknown = {}, bodyStatus = 200): void {
  harness.respond((call) =>
    call.path === STATUS_PATH ? json(ENABLED_STATUS) : json(body, bodyStatus),
  );
}

/**
 * Renders the hook's state as text and exposes each of its actions as a control,
 * so a spec drives the state machine directly.
 */
function Probe() {
  const { confirmAction, error, regeneratedCodes, openConfirm, submitConfirm } = useMfaSettings();
  return (
    <div>
      <output data-testid="probe-confirm">{confirmAction ?? "closed"}</output>
      <output data-testid="probe-error">{error ?? "none"}</output>
      <output data-testid="probe-codes">{regeneratedCodes?.join(",") ?? "none"}</output>
      <button
        type="button"
        data-testid="probe-open-disable"
        onClick={() => {
          openConfirm("disable");
        }}
      >
        open disable
      </button>
      <button
        type="button"
        data-testid="probe-open-regenerate"
        onClick={() => {
          openConfirm("regenerate");
        }}
      >
        open regenerate
      </button>
      <button
        type="button"
        data-testid="probe-submit"
        onClick={() => {
          void submitConfirm(PASSWORD);
        }}
      >
        submit
      </button>
    </div>
  );
}

describe("useMfaSettings", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("keeps the confirm panel open when the write fails, so the attempt can be retried", async () => {
    program({ succeeded: false, error: "invalid_password" }, 400);
    renderWithWallow(<Probe />, { harness });

    await userEvent.click(page.getByTestId("probe-open-disable"));
    await userEvent.click(page.getByTestId("probe-submit"));

    await expect
      .element(page.getByTestId("probe-error"))
      .toHaveTextContent("That password is incorrect.");
    await expect.element(page.getByTestId("probe-confirm")).toHaveTextContent("disable");
  });

  it("clears the revealed codes when the next confirm opens", async () => {
    program({ codes: ["aa-11", "bb-22"] });
    renderWithWallow(<Probe />, { harness });

    await userEvent.click(page.getByTestId("probe-open-regenerate"));
    await userEvent.click(page.getByTestId("probe-submit"));
    await expect.element(page.getByTestId("probe-codes")).toHaveTextContent("aa-11,bb-22");

    // Codes belong to the regenerate that minted them. Left on screen beside a
    // fresh prompt they read as if they were about to be reissued.
    await userEvent.click(page.getByTestId("probe-open-disable"));

    await expect.element(page.getByTestId("probe-codes")).toHaveTextContent("none");
  });

  it("clears the previous failure when the next confirm opens", async () => {
    program({ succeeded: false, error: "invalid_password" }, 400);
    renderWithWallow(<Probe />, { harness });

    await userEvent.click(page.getByTestId("probe-open-disable"));
    await userEvent.click(page.getByTestId("probe-submit"));
    await expect
      .element(page.getByTestId("probe-error"))
      .toHaveTextContent("That password is incorrect.");

    await userEvent.click(page.getByTestId("probe-open-regenerate"));

    await expect.element(page.getByTestId("probe-error")).toHaveTextContent("none");
  });

  it("sends nothing when a confirm is submitted with no panel open", async () => {
    program({ codes: ["aa-11"] });
    renderWithWallow(<Probe />, { harness });

    await expect.element(page.getByTestId("probe-confirm")).toHaveTextContent("closed");
    await userEvent.click(page.getByTestId("probe-submit"));

    // The sync point is a write that DID happen: by the time the regenerate has
    // landed, a request from the closed-panel click would have landed too.
    await userEvent.click(page.getByTestId("probe-open-regenerate"));
    await userEvent.click(page.getByTestId("probe-submit"));
    await expect.element(page.getByTestId("probe-codes")).toHaveTextContent("aa-11");

    expect(harness.calls.filter((call) => call.method === "POST")).toHaveLength(1);
    expect(harness.calls.find((call) => call.method === "POST")?.path).toBe(REGENERATE_PATH);
  });
});
