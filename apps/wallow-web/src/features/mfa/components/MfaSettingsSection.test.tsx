import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
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
 * Data flows through the SDK query layer (`mfaQueries.status()` +
 * `disableMfaMutation`/`regenerateBackupCodesMutation`), so the network seam is
 * the shared SDK client's `fetch`, overridden per test via `installSdkClientMock`
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path). Status is seeded via `setQueryData(['mfa', 'status'], ...)` (the key
 * `mfaQueries.status()` uses; `staleTime: Infinity` keeps the seed from
 * refetching), and the loading state by a never-settling request (`sdk.pending()`).
 * The disable/regenerate wire requests (`POST /api/v1/identity/mfa/disable`,
 * `.../backup-codes/regenerate`) are asserted via the recorded outgoing request
 * (`sdk.calls`) and invalidation on the live client's `invalidateQueries`. The
 * error surface exercises the REAL wire shape: MFA controllers return failures as
 * a raw `{ succeeded: false, error }` body (NOT ProblemDetails), so `rejectJson`
 * plays that body back and `unwrap()` throws it raw into `onError`.
 */

const DISABLED_STATUS = { enabled: false, method: null, backupCodeCount: 0 };
const ENABLED_STATUS = { enabled: true, method: "totp", backupCodeCount: 7 };

const DISABLE_PATH = "/api/v1/identity/mfa/disable";
const REGENERATE_PATH = "/api/v1/identity/mfa/backup-codes/regenerate";

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body ?? null), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
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
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders the loading state while the status query is pending", async () => {
    const client = newClient();
    // Never-settling request keeps the query pending.
    sdk.pending();

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

  it("submitting the disable confirm POSTs the entered password to the disable endpoint", async () => {
    // Disable resolves, then the success-invalidation refetches status; keep it Enabled.
    sdk.resolveJson(ENABLED_STATUS);
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-disable")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-disable"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      const disableCall = sdk.calls.find((c) => c.method === "POST" && c.path === DISABLE_PATH);
      expect(disableCall).toBeDefined();
      expect(disableCall?.body).toEqual({ password: "hunter2" });
    });
  });

  it("reveals the shared confirm panel when regenerate is clicked", async () => {
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));

    await expect.element(page.getByTestId("settings-mfa-confirm-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("settings-mfa-confirm-submit")).toBeInTheDocument();
  });

  it("submitting the regenerate confirm POSTs the entered password to the regenerate endpoint", async () => {
    // Regenerate resolves codes; the success-invalidation refetch keeps status Enabled.
    sdk.respond((call) =>
      call.path === REGENERATE_PATH ? json({ codes: ["z1", "z2"] }) : json(ENABLED_STATUS),
    );
    renderWithClient(clientWithStatus(ENABLED_STATUS), <MfaSettingsSection />);

    await expect.element(page.getByTestId("settings-mfa-regenerate")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("settings-mfa-regenerate"));
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    await vi.waitFor(() => {
      const regenCall = sdk.calls.find((c) => c.method === "POST" && c.path === REGENERATE_PATH);
      expect(regenCall).toBeDefined();
      expect(regenCall?.body).toEqual({ password: "hunter2" });
    });
  });

  it("invalidates ['mfa', 'status'] after a successful disable", async () => {
    const client = clientWithStatus(ENABLED_STATUS);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    sdk.resolveJson(ENABLED_STATUS);
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
    // The status query is seeded (staleTime Infinity) so only the disable POST
    // hits the seam; onError does not invalidate, so there is no status refetch.
    sdk.rejectJson({ succeeded: false, error: "invalid_password" }, 400);
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
    sdk.rejectJson({ succeeded: false, error: "invalid_password" }, 400);
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
  // The regenerated codes must be revealed in a "New Backup Codes (save these
  // somewhere safe)" panel after a successful regenerate. The resolved
  // `{ codes: string[] }` payload must be surfaced once under
  // `settings-mfa-regenerated-codes`, not silently discarded.
  it("reveals the regenerated backup codes under settings-mfa-regenerated-codes after a successful regenerate", async () => {
    // Regenerate resolves the new codes; the post-success invalidation refetch of
    // status must stay Enabled so the card does not flip mid-reveal.
    sdk.respond((call) =>
      call.path === REGENERATE_PATH
        ? json({ codes: ["new-code-1", "new-code-2", "new-code-3"] })
        : json(ENABLED_STATUS),
    );
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
