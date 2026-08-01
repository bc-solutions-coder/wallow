import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { mfaGetStatusQueryKey } from "../api";
import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * The MFA settings status card: Enabled/Disabled, the shared confirm panel behind
 * both disable and regenerate, and the regenerated-codes reveal.
 *
 * The post-success sweep targets the status OPERATION, not its `Identity` tag,
 * which spans the whole identity module. MFA controllers return failures as a raw
 * `{ succeeded: false, error }` body rather than RFC 7807, so the error surface
 * maps the `error` code instead of reading a `.detail`.
 */

const DISABLED_STATUS = { enabled: false, method: null, backupCodeCount: 0 };
const ENABLED_STATUS = { enabled: true, method: "totp", backupCodeCount: 7 };

const STATUS_PATH = "/api/v1/identity/mfa/status";
const DISABLE_PATH = "/api/v1/identity/mfa/disable";
const REGENERATE_PATH = "/api/v1/identity/mfa/backup-codes/regenerate";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body ?? null), {
    status,
    headers: { "content-type": "application/json" },
  });
}

/**
 * Answer the status query with `status` and every other request (the
 * disable/regenerate POST, plus the refetch a successful sweep triggers) with
 * `body` at `bodyStatus`. Path-aware because a single blanket responder cannot
 * both keep the card Enabled and reject the mutation the spec is driving.
 */
function programStatus(status: unknown, body: unknown = {}, bodyStatus = 200): void {
  harness.respond((call) => (call.path === STATUS_PATH ? json(status) : json(body, bodyStatus)));
}

/** Program the status seam, then render the card. */
function renderStatus(status: unknown, body: unknown = {}, bodyStatus = 200) {
  programStatus(status, body, bodyStatus);
  return renderWithWallow(<MfaSettingsSection />, { harness });
}

describe("MfaSettingsSection", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the loading state while the status query is pending", async () => {
    // Never-settling request keeps the query pending.
    harness.pending();

    renderWithWallow(<MfaSettingsSection />, { harness });

    await expect
      .element(page.getByTestId("settings-mfa-loading"))
      .toHaveTextContent("Loading MFA status…");
  });

  it("shows Disabled with the enable affordance and no enabled-only controls when MFA is off", async () => {
    renderStatus(DISABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Disabled");
    await expect.element(page.getByText("Multi-Factor Authentication")).toBeInTheDocument();
    await expect.element(page.getByText("Status")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-enable")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-disable")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-regenerate")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-backup-count")).not.toBeInTheDocument();
  });

  it("shows Enabled with the backup-code count, disable, and regenerate affordances when MFA is on", async () => {
    renderStatus(ENABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Enabled");
    await expect.element(page.getByText("Backup Codes Remaining")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-backup-count")).toHaveTextContent("7");
    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-enable")).not.toBeInTheDocument();
  });

  it("enters the inline enroll flow when enable is clicked", async () => {
    renderStatus(DISABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-enable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-enable"));

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
  });

  it("reveals the shared confirm panel when disable is clicked", async () => {
    renderStatus(ENABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));

    await expect.element(page.getByTestId("settings-mfa-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-submit")).toBeInTheDocument();
  });

  it("submitting the disable confirm POSTs the entered password to the disable endpoint", async () => {
    renderStatus(ENABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      const disableCall = harness.calls.find((c) => c.method === "POST" && c.path === DISABLE_PATH);
      expect(disableCall).toBeDefined();
      expect(disableCall?.body).toEqual({ password: "hunter2" });
    });
  });

  it("reveals the shared confirm panel when regenerate is clicked", async () => {
    renderStatus(ENABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));

    await expect.element(page.getByTestId("settings-mfa-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-submit")).toBeInTheDocument();
  });

  it("submitting the regenerate confirm POSTs the entered password to the regenerate endpoint", async () => {
    renderStatus(ENABLED_STATUS, { codes: ["z1", "z2"] });

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      const regenCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === REGENERATE_PATH,
      );
      expect(regenCall).toBeDefined();
      expect(regenCall?.body).toEqual({ password: "hunter2" });
    });
  });

  it("sweeps the MFA status query after a successful disable", async () => {
    const { queryClient } = renderStatus(ENABLED_STATUS);
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await expectSwept(invalidateSpy, mfaGetStatusQueryKey());
  });

  it("surfaces the mapped error message in settings-mfa-error when disable rejects with the real { succeeded:false, error } body", async () => {
    // Only the disable POST fails; the status request keeps answering Enabled so
    // the card stays on the branch that owns the confirm panel.
    renderStatus(ENABLED_STATUS, { succeeded: false, error: "invalid_password" }, 400);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "wrong");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    const error = page.getByTestId("settings-mfa-error");
    await expect.element(error).toHaveTextContent("That password is incorrect.");
    await expect.element(error).not.toHaveTextContent("Unable to complete that action.");
  });

  it("surfaces the mapped error message in settings-mfa-error when regenerate rejects with the real { succeeded:false, error } body", async () => {
    renderStatus(ENABLED_STATUS, { succeeded: false, error: "invalid_password" }, 400);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "wrong");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    const error = page.getByTestId("settings-mfa-error");
    await expect.element(error).toHaveTextContent("That password is incorrect.");
    await expect.element(error).not.toHaveTextContent("Unable to complete that action.");
  });

  // Regenerating invalidates the OLD codes, so the resolved `{ codes }` payload
  // has to be revealed rather than silently discarded.
  it("reveals the regenerated backup codes under settings-mfa-regenerated-codes after a successful regenerate", async () => {
    renderStatus(ENABLED_STATUS, { codes: ["new-code-1", "new-code-2", "new-code-3"] });

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    const codes = page.getByTestId("settings-mfa-regenerated-codes");
    await expect.element(codes).toHaveTextContent("new-code-1");
    await expect.element(codes).toHaveTextContent("new-code-2");
    await expect.element(codes).toHaveTextContent("new-code-3");
    await expect
      .element(
        page.getByText(
          "New backup codes — save these somewhere safe. They will not be shown again.",
        ),
      )
      .toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-password")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-error")).not.toBeInTheDocument();
  });
});
