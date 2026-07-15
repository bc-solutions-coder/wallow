/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MfaEnrollFlow } from "./MfaEnrollFlow";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention for wallow-web's RTL tests.
expect.extend(matchers);

/**
 * Component spec for the MFA enroll flow (Wallow-8w1h.6.4). Mirrors the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/MfaEnroll.razor` step machine and
 * the C# E2E page object `MfaEnrollPage`:
 *
 *   setup  -> `mfa-enroll-begin-setup` runs `enrollTotp`
 *   verify -> `mfa-enroll-secret` + `mfa-enroll-qr` + `mfa-enroll-code`
 *             + `mfa-enroll-submit` runs `confirmEnroll(secret, code)`
 *   done   -> `mfa-enroll-backup-codes` revealed ONCE + Done action
 *
 * `mfa-enroll-error` surfaces any step's failure (RFC 7807 `detail` or the
 * `{ succeeded: false }` confirm code); `mfa-enroll-cancel` is always visible.
 *
 * The `getWallowSdk()` facade is mocked so `mfa.enrollTotp`/`mfa.confirmEnroll`
 * are spies; the component builds its mutations from the api.ts factories, so
 * the confirm `onSuccess` invalidation of `['mfa', 'status']` is observed by
 * spying on the live client's `invalidateQueries`.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  status: vi.fn(),
  enrollTotp: vi.fn(),
  confirmEnroll: vi.fn(),
  disable: vi.fn(),
  regenerateBackupCodes: vi.fn(),
}));

// Mock the facade the feature's api.ts imports (`../../lib/wallow-sdk` from
// features/mfa; `../../../lib/wallow-sdk` from this test file). Both specifiers
// resolve to the same module id, so this single mock covers api.ts too.
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    mfa: {
      status: mocks.status,
      enrollTotp: mocks.enrollTotp,
      confirmEnroll: mocks.confirmEnroll,
      disable: mocks.disable,
      regenerateBackupCodes: mocks.regenerateBackupCodes,
    },
  }),
}));

const ENROLL_RESPONSE = {
  secret: "JBSWY3DPEHPK3PXP",
  qrUri: "otpauth://totp/Wallow:ada@lovelace.io?secret=JBSWY3DPEHPK3PXP&issuer=Wallow",
};

const CONFIRM_SUCCESS = {
  succeeded: true,
  backupCodes: ["aaaa-1111", "bbbb-2222", "cccc-3333"],
};

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** Drive the flow from the initial setup CTA to the revealed secret. */
async function beginEnrollment(user: ReturnType<typeof userEvent.setup>) {
  mocks.enrollTotp.mockResolvedValue(ENROLL_RESPONSE);
  await user.click(screen.getByTestId("mfa-enroll-begin-setup"));
  await screen.findByTestId("mfa-enroll-secret");
}

describe("MfaEnrollFlow", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the begin-setup CTA and the always-visible cancel affordance initially", () => {
    renderWithClient(newClient(), <MfaEnrollFlow />);

    expect(screen.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    expect(screen.getByTestId("mfa-enroll-cancel")).toBeInTheDocument();
  });

  it("does NOT show the secret, QR, code input, or backup codes before setup begins", () => {
    renderWithClient(newClient(), <MfaEnrollFlow />);

    expect(screen.queryByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-qr")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-code")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-submit")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("clicking begin-setup calls enrollTotp and reveals the secret, QR, code input, and submit", async () => {
    const user = userEvent.setup();
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);

    expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("mfa-enroll-secret")).toHaveTextContent("JBSWY3DPEHPK3PXP");
    expect(screen.getByTestId("mfa-enroll-qr")).toBeInTheDocument();
    expect(screen.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(screen.getByTestId("mfa-enroll-submit")).toBeInTheDocument();
    // The begin-setup CTA is replaced by the verify step.
    expect(screen.queryByTestId("mfa-enroll-begin-setup")).not.toBeInTheDocument();
  });

  it("submitting the code calls confirmEnroll with the enrolled secret and the entered code", async () => {
    const user = userEvent.setup();
    mocks.confirmEnroll.mockResolvedValue(CONFIRM_SUCCESS);
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "123456");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    await waitFor(() => {
      expect(mocks.confirmEnroll).toHaveBeenCalledTimes(1);
    });
    expect(mocks.confirmEnroll).toHaveBeenCalledWith("JBSWY3DPEHPK3PXP", "123456");
  });

  it("reveals the one-time backup codes (one child per code) after a successful confirm", async () => {
    const user = userEvent.setup();
    mocks.confirmEnroll.mockResolvedValue(CONFIRM_SUCCESS);
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "123456");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    const codes = await screen.findByTestId("mfa-enroll-backup-codes");
    expect(codes).toHaveTextContent("aaaa-1111");
    expect(codes).toHaveTextContent("bbbb-2222");
    expect(codes).toHaveTextContent("cccc-3333");
    // The secret + code input are gone once codes are shown (one-time reveal).
    expect(screen.queryByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-code")).not.toBeInTheDocument();
  });

  it("invalidates ['mfa', 'status'] after a successful confirm so the card flips to Enabled", async () => {
    const user = userEvent.setup();
    const client = newClient();
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.confirmEnroll.mockResolvedValue(CONFIRM_SUCCESS);
    renderWithClient(client, <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "123456");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["mfa", "status"] });
    });
  });

  it("fires onDone when the Done action is clicked after the backup codes are shown", async () => {
    const user = userEvent.setup();
    const onDone = vi.fn();
    mocks.confirmEnroll.mockResolvedValue(CONFIRM_SUCCESS);
    renderWithClient(newClient(), <MfaEnrollFlow onDone={onDone} />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "123456");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    await screen.findByTestId("mfa-enroll-backup-codes");
    await user.click(screen.getByTestId("mfa-enroll-done"));

    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it("fires onCancel when the cancel affordance is clicked", async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    renderWithClient(newClient(), <MfaEnrollFlow onCancel={onCancel} />);

    await user.click(screen.getByTestId("mfa-enroll-cancel"));

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
    const user = userEvent.setup();
    // The real thrown shape from EnrollTotp's Unauthorized branch.
    mocks.enrollTotp.mockRejectedValue({ succeeded: false, error: "no_auth_session" });
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await user.click(screen.getByTestId("mfa-enroll-begin-setup"));

    const error = await screen.findByTestId("mfa-enroll-error");
    // The specific reason the backend supplied must reach the user — not the
    // generic "Unable to start MFA enrollment." fallback.
    expect(error).toHaveTextContent("Your session has expired. Please sign in again.");
    expect(error).not.toHaveTextContent("Unable to start MFA enrollment.");
    // No secret is revealed on a failed enroll.
    expect(screen.queryByTestId("mfa-enroll-secret")).not.toBeInTheDocument();
  });

  it("surfaces the mapped error message in mfa-enroll-error when confirm rejects with the real { succeeded:false, error } body", async () => {
    const user = userEvent.setup();
    // The real thrown shape from ConfirmEnrollment's Unauthorized branch (a 401
    // that unwrap() throws — NOT a resolved { succeeded:false } payload).
    mocks.confirmEnroll.mockRejectedValue({ succeeded: false, error: "no_auth_session" });
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "000000");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    const error = await screen.findByTestId("mfa-enroll-error");
    expect(error).toHaveTextContent("Your session has expired. Please sign in again.");
    // Not the generic confirm fallback ("That verification code is not valid.").
    expect(error).not.toHaveTextContent("That verification code is not valid.");
    expect(screen.queryByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("maps a rejected confirm invalid_code to the verification-code message", async () => {
    const user = userEvent.setup();
    // invalid_code is a 400 BadRequest in production, so it arrives via onError
    // (thrown), not as a resolved { succeeded:false } payload.
    mocks.confirmEnroll.mockRejectedValue({ succeeded: false, error: "invalid_code" });
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "000000");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    const error = await screen.findByTestId("mfa-enroll-error");
    expect(error).toHaveTextContent("That verification code is not valid.");
    expect(screen.queryByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });

  it("shows an error and does NOT reveal backup codes when confirm returns { succeeded: false }", async () => {
    const user = userEvent.setup();
    // The HTTP call resolves, but the enrollment was rejected (e.g. wrong code).
    mocks.confirmEnroll.mockResolvedValue({ succeeded: false, error: "invalid_code" });
    renderWithClient(newClient(), <MfaEnrollFlow />);

    await beginEnrollment(user);
    await user.type(screen.getByTestId("mfa-enroll-code"), "999999");
    await user.click(screen.getByTestId("mfa-enroll-submit"));

    expect(await screen.findByTestId("mfa-enroll-error")).toBeInTheDocument();
    expect(screen.queryByTestId("mfa-enroll-backup-codes")).not.toBeInTheDocument();
  });
});
