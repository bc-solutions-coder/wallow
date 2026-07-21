import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * Component spec for the MFA settings status card (Wallow-8w1h.6.4). Mirrors the
 * Blazor oracle `api/src/Wallow.Web/Components/Shared/MfaSettingsSection.razor`
 * and the C# E2E page object `SettingsMfaSection`:
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
 * Status is seeded via `setQueryData(['mfa', 'status'], ...)` (the key
 * `mfaQueries.status()` uses); the loading state is driven by a never-resolving
 * facade `status` call. The `getWallowSdk()` facade is mocked so the
 * disable/regenerate mutations are spies; invalidation is observed on the live
 * client.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  status: vi.fn(),
  enrollTotp: vi.fn(),
  confirmEnroll: vi.fn(),
  disable: vi.fn(),
  regenerateBackupCodes: vi.fn(),
}));

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

const DISABLED_STATUS = { enabled: false, method: null, backupCodeCount: 0 };
const ENABLED_STATUS = { enabled: true, method: "totp", backupCodeCount: 7 };

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function clientWithStatus(status: unknown): QueryClient {
  const client = newClient();
  client.setQueryData(["mfa", "status"], status);
  return client;
}

describe("MfaSettingsSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the loading state while the status query is pending", async () => {
    const client = newClient();
    // Never-resolving facade call keeps the query pending.
    mocks.status.mockReturnValue(new Promise(() => {}));

    renderWithClient(client, <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-loading")).toBeInTheDocument();
  });

  it("shows Disabled with the enable affordance and no enabled-only controls when MFA is off", async () => {
    renderWithClient(clientWithStatus(DISABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Disabled");
    await expect.element(page.getByTestId("settings-mfa-enable")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-disable")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-regenerate")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-backup-count")).not.toBeInTheDocument();
  });

  it("shows Enabled with the backup-code count, disable, and regenerate affordances when MFA is on", async () => {
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-status")).toHaveTextContent("Enabled");
    await expect.element(page.getByTestId("settings-mfa-backup-count")).toHaveTextContent("7");
    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-enable")).not.toBeInTheDocument();
  });

  it("enters the inline enroll flow when enable is clicked", async () => {
    renderWithClient(clientWithStatus(DISABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-enable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-enable"));

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
  });

  it("reveals the shared confirm panel when disable is clicked", async () => {
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));

    await expect.element(page.getByTestId("settings-mfa-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-submit")).toBeInTheDocument();
  });

  it("submitting the disable confirm calls disable with the entered password", async () => {
    mocks.disable.mockResolvedValue({ succeeded: true });
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      expect(mocks.disable).toHaveBeenCalledWith("hunter2");
    });
  });

  it("reveals the shared confirm panel when regenerate is clicked", async () => {
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));

    await expect.element(page.getByTestId("settings-mfa-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-submit")).toBeInTheDocument();
  });

  it("submitting the regenerate confirm calls regenerateBackupCodes with the entered password", async () => {
    mocks.regenerateBackupCodes.mockResolvedValue({ codes: ["z1", "z2"] });
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      expect(mocks.regenerateBackupCodes).toHaveBeenCalledWith("hunter2");
    });
  });

  it("invalidates ['mfa', 'status'] after a successful disable", async () => {
    const client = clientWithStatus(ENABLED_STATUS);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.disable.mockResolvedValue({ succeeded: true });
    renderWithClient(client, <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["mfa", "status"] });
    });
  });

  // REAL WIRE SHAPE (Wallow-8w1h.6.6): MfaController.Disable /
  // RegenerateBackupCodes return their failures as a raw anonymous object
  // `{ succeeded: false, error: "<code>" }` (e.g. invalid_password), NOT an
  // RFC 7807 ProblemDetails. `unwrap()` THROWS that raw body on the 400, so
  // onError receives `{ succeeded:false, error }` with NO `.detail`. The error
  // surface must map that `error` code to a meaningful message instead of
  // always showing the generic "Unable to complete that action." fallback.
  it("surfaces the mapped error message in settings-mfa-error when disable rejects with the real { succeeded:false, error } body", async () => {
    mocks.disable.mockRejectedValue({ succeeded: false, error: "invalid_password" });
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

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
    mocks.regenerateBackupCodes.mockRejectedValue({ succeeded: false, error: "invalid_password" });
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

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
  // The Blazor oracle (MfaSettingsSection.razor) reveals `_regeneratedCodes` in a
  // "New Backup Codes (save these somewhere safe)" panel after a successful
  // regenerate. The resolved `{ codes: string[] }` payload must be surfaced once
  // under `settings-mfa-regenerated-codes`, not silently discarded.
  it("reveals the regenerated backup codes under settings-mfa-regenerated-codes after a successful regenerate", async () => {
    // Keep status Enabled across the post-success invalidation refetch.
    mocks.status.mockResolvedValue(ENABLED_STATUS);
    mocks.regenerateBackupCodes.mockResolvedValue({
      codes: ["new-code-1", "new-code-2", "new-code-3"],
    });
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

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
