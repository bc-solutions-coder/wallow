import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "../../../test/invalidation";
import { mfaGetStatusQueryKey } from "../api";
import { MfaEnrollFlow } from "./MfaEnrollFlow";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Component spec for the MFA enroll flow (Wallow-8w1h.6.4). Exercises the enroll
 * step machine against the C# E2E page object `MfaEnrollPage`:
 *
 *   setup  -> `mfa-enroll-begin-setup` runs `enrollTotp`
 *   verify -> `mfa-enroll-secret` + `mfa-enroll-qr` + `mfa-enroll-code`
 *             + `mfa-enroll-submit` runs `confirmEnroll(secret, code)`
 *   done   -> `mfa-enroll-backup-codes` revealed ONCE + Done action
 *
 * `mfa-enroll-error` surfaces any step's failure (RFC 7807 `detail` or the
 * `{ succeeded: false }` confirm code); `mfa-enroll-cancel` is always visible.
 *
 * Data flows through the GENERATED mutations (`mfaEnrollTotpMutation` /
 * `mfaConfirmEnrollmentMutation`), so the network seam is the SDK instance the
 * render puts on the router context, backed by `createSdkHarness()`
 * (Wallow-pu6a.5.5). The enroll begin (`POST /api/v1/identity/mfa/enroll/totp`)
 * and confirm (`.../enroll/confirm`) requests are programmed with a path-aware
 * responder so a begin -> confirm sequence resolves each step independently; the
 * confirm's `onSuccess` sweep is checked by running the filter it handed
 * `invalidateQueries` against the real `mfaGetStatusQueryKey()` (`expectSwept`) —
 * the status operation specifically, not its `Identity` tag, which spans the
 * whole module. Error specs replay the REAL wire shape: MFA controllers return
 * failures as a raw `{ succeeded: false, error }` body (NOT ProblemDetails),
 * which the SDK's error interceptor passes through to `onError`.
 */

const ENROLL_RESPONSE = {
  secret: "JBSWY3DPEHPK3PXP",
  qrUri: "otpauth://totp/Wallow:ada@lovelace.io?secret=JBSWY3DPEHPK3PXP&issuer=Wallow",
};

const CONFIRM_SUCCESS = {
  succeeded: true,
  backupCodes: ["aaaa-1111", "bbbb-2222", "cccc-3333"],
};

const TOTP_PATH = "/api/v1/identity/mfa/enroll/totp";
const CONFIRM_PATH = "/api/v1/identity/mfa/enroll/confirm";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body ?? null), {
    status,
    headers: { "content-type": "application/json" },
  });
}

/**
 * Program the enroll seam: `enrollTotp` always resolves the one-time secret + QR;
 * the confirm step returns `confirmBody` at `confirmStatus` (default 200) so a
 * begin -> confirm sequence over the single shared responder plays each step
 * independently.
 */
function programFlow(confirmBody: unknown = CONFIRM_SUCCESS, confirmStatus = 200) {
  harness.respond((call) => {
    if (call.path === TOTP_PATH) {
      return json(ENROLL_RESPONSE);
    }
    if (call.path === CONFIRM_PATH) {
      return json(confirmBody, confirmStatus);
    }
    return json({});
  });
}

/** Drive the flow from the initial setup CTA to the revealed secret. */
async function beginEnrollment() {
  await userEvent.click(page.getByTestId("mfa-enroll-begin-setup"));
  await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
}

describe("MfaEnrollFlow", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the begin-setup CTA and the always-visible cancel affordance initially", async () => {
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-cancel")).toBeInTheDocument();
  });

  it("does NOT show the secret, QR, code input, or backup codes before setup begins", async () => {
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await expect.element(page.getByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-qr")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-code")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-submit")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("clicking begin-setup calls enrollTotp and reveals the secret, QR, code input, and submit", async () => {
    programFlow();
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();

    const totpCall = harness.calls.find((c) => c.method === "POST" && c.path === TOTP_PATH);
    expect(totpCall).toBeDefined();
    await expect
      .element(page.getByTestId("mfa-enroll-secret"))
      .toHaveTextContent("JBSWY3DPEHPK3PXP");
    await expect.element(page.getByTestId("mfa-enroll-qr")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeInTheDocument();
    // The begin-setup CTA is replaced by the verify step.
    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).not.toBeInTheDocument();
  });

  it("submitting the code calls confirmEnroll with the enrolled secret and the entered code", async () => {
    programFlow();
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "123456");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    await vi.waitFor(() => {
      const confirmCall = harness.calls.find((c) => c.method === "POST" && c.path === CONFIRM_PATH);
      expect(confirmCall).toBeDefined();
      expect(confirmCall?.body).toEqual({ secret: "JBSWY3DPEHPK3PXP", code: "123456" });
    });
  });

  it("reveals the one-time backup codes (one child per code) after a successful confirm", async () => {
    programFlow();
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "123456");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    const codes = page.getByTestId("mfa-enroll-backup-codes");
    await expect.element(codes).toHaveTextContent("aaaa-1111");
    await expect.element(codes).toHaveTextContent("bbbb-2222");
    await expect.element(codes).toHaveTextContent("cccc-3333");
    // The secret + code input are gone once codes are shown (one-time reveal).
    await expect.element(page.getByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-code")).not.toBeInTheDocument();
  });

  it("sweeps the MFA status query after a successful confirm so the card flips to Enabled", async () => {
    programFlow();
    const { queryClient } = renderWithWallow(<MfaEnrollFlow />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "123456");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    await expectSwept(invalidateSpy, mfaGetStatusQueryKey());
  });

  it("fires onDone when the Done action is clicked after the backup codes are shown", async () => {
    const onDone = vi.fn();
    programFlow();
    renderWithWallow(<MfaEnrollFlow onDone={onDone} />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "123456");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("mfa-enroll-done"));

    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it("fires onCancel when the cancel affordance is clicked", async () => {
    const onCancel = vi.fn();
    renderWithWallow(<MfaEnrollFlow onCancel={onCancel} />, { harness });

    await userEvent.click(page.getByTestId("mfa-enroll-cancel"));

    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  // REAL WIRE SHAPE (Wallow-8w1h.6.6): the MFA endpoints return their business
  // failures as a raw anonymous object `{ succeeded: false, error: "<code>" }`
  // (see MfaController EnrollTotp/ConfirmEnrollment), NOT an RFC 7807
  // ProblemDetails body. `unwrap()` THROWS that raw body on any non-2xx status,
  // so the component's onError receives `{ succeeded: false, error }` with NO
  // `.detail`. The error surface must map that `error` code to a meaningful
  // message instead of always falling back to the generic step text.
  it("surfaces the mapped error message in mfa-enroll-error when enrollTotp rejects with the real { succeeded:false, error } body", async () => {
    // The real thrown shape from EnrollTotp's Unauthorized branch.
    harness.respond((call) =>
      call.path === TOTP_PATH
        ? json({ succeeded: false, error: "no_auth_session" }, 401)
        : json({}),
    );
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await userEvent.click(page.getByTestId("mfa-enroll-begin-setup"));

    const error = page.getByTestId("mfa-enroll-error");
    // The specific reason the backend supplied must reach the user — not the
    // generic "Unable to start MFA enrollment." fallback.
    await expect
      .element(error)
      .toHaveTextContent("Your session has expired. Please sign in again.");
    await expect.element(error).not.toHaveTextContent("Unable to start MFA enrollment.");
    // No secret is revealed on a failed enroll.
    await expect.element(page.getByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
  });

  it("surfaces the mapped error message in mfa-enroll-error when confirm rejects with the real { succeeded:false, error } body", async () => {
    // The real thrown shape from ConfirmEnrollment's Unauthorized branch (a 401
    // that unwrap() throws — NOT a resolved { succeeded:false } payload).
    programFlow({ succeeded: false, error: "no_auth_session" }, 401);
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "000000");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    const error = page.getByTestId("mfa-enroll-error");
    await expect
      .element(error)
      .toHaveTextContent("Your session has expired. Please sign in again.");
    // Not the generic confirm fallback ("That verification code is not valid.").
    await expect.element(error).not.toHaveTextContent("That verification code is not valid.");
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("maps a rejected confirm invalid_code to the verification-code message", async () => {
    // invalid_code is a 400 BadRequest in production, so it arrives via onError
    // (thrown), not as a resolved { succeeded:false } payload.
    programFlow({ succeeded: false, error: "invalid_code" }, 400);
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "000000");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toHaveTextContent("That verification code is not valid.");
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("shows an error and does NOT reveal backup codes for any other { succeeded: false } rejection", async () => {
    // Every MfaController rejection is a 4xx carrying `{ succeeded, error }`;
    // an error code the flow has no bespoke message for still has to surface
    // something and must never reveal codes. `update_failed` is that case.
    programFlow({ succeeded: false, error: "update_failed" }, 400);
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "999999");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });
});
