import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { mfaGetStatusQueryKey } from "../api";
import { MfaEnrollFlow } from "./MfaEnrollFlow";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * The MFA enroll step machine: setup -> verify (secret + QR + code) -> done
 * (backup codes revealed ONCE).
 *
 * The confirm sweep targets the status OPERATION, not its `Identity` tag, which
 * spans the whole identity module. MFA controllers return their failures as a
 * raw `{ succeeded: false, error }` body rather than RFC 7807, thrown on any
 * non-2xx, so `onError` sees no `.detail` and the surface maps the code.
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

  it("surfaces the mapped error message in mfa-enroll-error when enrollTotp rejects with the real { succeeded:false, error } body", async () => {
    // The thrown shape from EnrollTotp's Unauthorized branch.
    harness.respond((call) =>
      call.path === TOTP_PATH
        ? json({ succeeded: false, error: "no_auth_session" }, 401)
        : json({}),
    );
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await userEvent.click(page.getByTestId("mfa-enroll-begin-setup"));

    const error = page.getByTestId("mfa-enroll-error");
    await expect
      .element(error)
      .toHaveTextContent("Your session has expired. Please sign in again.");
    await expect.element(error).not.toHaveTextContent("Unable to start MFA enrollment.");
    await expect.element(page.getByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
  });

  it("surfaces the mapped error message in mfa-enroll-error when confirm rejects with the real { succeeded:false, error } body", async () => {
    // ConfirmEnrollment's Unauthorized branch is a 401 that unwrap() THROWS, not
    // a resolved `{ succeeded: false }` payload.
    programFlow({ succeeded: false, error: "no_auth_session" }, 401);
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "000000");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    const error = page.getByTestId("mfa-enroll-error");
    await expect
      .element(error)
      .toHaveTextContent("Your session has expired. Please sign in again.");
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
    // An error code the flow has no bespoke message for still has to surface
    // something, and must never reveal codes. `update_failed` is that case.
    programFlow({ succeeded: false, error: "update_failed" }, 400);
    renderWithWallow(<MfaEnrollFlow />, { harness });

    await beginEnrollment();
    await userEvent.type(page.getByTestId("mfa-enroll-code"), "999999");
    await userEvent.click(page.getByTestId("mfa-enroll-submit"));

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });
});
