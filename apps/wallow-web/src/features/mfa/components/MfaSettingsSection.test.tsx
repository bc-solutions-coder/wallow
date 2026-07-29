import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "../../../test/invalidation";
import { mfaGetStatusQueryKey } from "../api";
import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * Component spec for the MFA settings status card (Wallow-8w1h.6.4). Mirrors the
 * C# E2E page object `SettingsMfaSection`:
 *
 *   - `settings-mfa-status` ("Enabled"/"Disabled")
 *   - DISABLED: `settings-mfa-enable` -> enters the inline enroll flow
 *     (`mfa-enroll-begin-setup`); no cross-app redirect (same-origin SPA).
 *   - ENABLED: `settings-mfa-backup-count`, `settings-mfa-disable`,
 *     `settings-mfa-regenerate`; each opens the SHARED confirm panel
 *     (`settings-mfa-confirm-password` + `settings-mfa-confirm-submit`) driving
 *     the disable / regenerate mutations, which invalidate `['mfa', 'status']`.
 *   - `settings-mfa-error` — shared RFC 7807 error surface.
 *
 * Data flows through the GENERATED query surface (`mfaGetStatusOptions` +
 * `mfaDisableMutation`/`mfaRegenerateBackupCodesMutation`), so the network seam is
 * the SDK instance the render puts on the router context, backed by
 * `createSdkHarness()` (Wallow-pu6a.5.5). Status is no longer seeded into a cache
 * key — the status request is ANSWERED (`programStatus`), so the generated
 * operation and its parsing run as they do in the app — and the loading state
 * comes from a never-settling request (`harness.pending()`). The
 * disable/regenerate wire requests (`POST /api/v1/identity/mfa/disable`,
 * `.../backup-codes/regenerate`) are asserted via the recorded outgoing request
 * (`harness.calls`), and the post-success sweep by running the filter handed to
 * `invalidateQueries` against the real `mfaGetStatusQueryKey()` (`expectSwept`) —
 * the status operation specifically, not its `Identity` tag, which spans the whole
 * module. The error surface exercises the REAL wire shape: MFA controllers return
 * failures as a raw `{ succeeded: false, error }` body (NOT ProblemDetails), which
 * the SDK's error interceptor passes through to `onError`.
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

    await expect.element(page.getByTestId("settings-mfa-loading")).toBeInTheDocument();
  });

  it("shows Disabled with the enable affordance and no enabled-only controls when MFA is off", async () => {
    renderStatus(DISABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Disabled");
    await expect.element(page.getByTestId("settings-mfa-enable")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-disable")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-regenerate")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-backup-count")).not.toBeInTheDocument();
  });

  it("shows Enabled with the backup-code count, disable, and regenerate affordances when MFA is on", async () => {
    renderStatus(ENABLED_STATUS);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Enabled");
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

  // REAL WIRE SHAPE (Wallow-8w1h.6.6): MfaController.Disable /
  // RegenerateBackupCodes return their failures as a raw anonymous object
  // `{ succeeded: false, error: "<code>" }` (e.g. invalid_password), NOT an
  // RFC 7807 ProblemDetails. `unwrap()` THROWS that raw body on the 400, so
  // onError receives `{ succeeded:false, error }` with NO `.detail`. The error
  // surface must map that `error` code to a meaningful message instead of
  // always showing the generic "Unable to complete that action." fallback.
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
    // Not the generic "Unable to complete that action." fallback.
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

  // REGENERATED-CODES REVEAL (Wallow-8w1h.6.6): the whole point of regenerating
  // is that the OLD codes are invalidated and the user MUST save the NEW ones.
  // The regenerated codes must be revealed in a "New Backup Codes (save these
  // somewhere safe)" panel after a successful regenerate. The resolved
  // `{ codes: string[] }` payload must be surfaced once under
  // `settings-mfa-regenerated-codes`, not silently discarded.
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
    // The confirm panel closes once the new codes are revealed.
    await expect.element(page.getByTestId("settings-mfa-confirm-password")).not.toBeInTheDocument();
    // No error surface on success.
    await expect.element(page.getByTestId("settings-mfa-error")).not.toBeInTheDocument();
  });
});
