import { beforeEach, describe, expect, it, vi } from "vitest";
import type { QueryClient } from "@tanstack/react-query";

/**
 * MFA feature query layer (Wallow-8w1h.6.3) — copies the CANONICAL Organizations /
 * Apps `api.test.ts` spec. The `getWallowSdk()` facade is mocked: these tests
 * assert the query/mutation layer's KEY STABILITY, its DELEGATION to the facade
 * `mfa` slice, and its INVALIDATION model, not the wire.
 *
 * Invalidation model asserted here: confirm/disable/regenerate each invalidate
 * `['mfa', 'status']` on success; `enrollTotp` does NOT (its one-time secret+QR
 * is held in component state, status stays disabled until confirm).
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  status: vi.fn(),
  enrollTotp: vi.fn(),
  confirmEnroll: vi.fn(),
  disable: vi.fn(),
  regenerateBackupCodes: vi.fn(),
}));

// Route/component files import only from this feature's api.ts; api.ts in turn
// imports getWallowSdk. We mock the facade module so the slice methods are spies.
vi.mock("../../lib/wallow-sdk", () => ({
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

import {
  confirmEnrollMutation,
  disableMfaMutation,
  enrollTotpMutation,
  mfaQueries,
  regenerateBackupCodesMutation,
} from "./api";

/** Invoke a queryOptions `queryFn` while ignoring its QueryFunctionContext arg. */
async function callQueryFn(queryFn: unknown): Promise<unknown> {
  return (queryFn as () => Promise<unknown>)();
}

function fakeQueryClient(): QueryClient {
  return { invalidateQueries: vi.fn() } as unknown as QueryClient;
}

describe("mfaQueries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("status", () => {
    it("keys the status query as ['mfa', 'status']", () => {
      expect(mfaQueries.status().queryKey).toEqual(["mfa", "status"]);
    });

    it("keeps the status queryKey stable across calls", () => {
      expect(mfaQueries.status().queryKey).toEqual(mfaQueries.status().queryKey);
    });

    it("queryFn delegates to the facade mfa.status and returns its data", async () => {
      const status = { enabled: true, method: "totp", backupCodeCount: 8 };
      mocks.status.mockResolvedValue(status);

      const result = await callQueryFn(mfaQueries.status().queryFn);

      expect(mocks.status).toHaveBeenCalledTimes(1);
      expect(result).toBe(status);
    });
  });
});

describe("enrollTotpMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to the facade mfa.enrollTotp and returns its data", async () => {
    const enroll = { secret: "ABC", qrUri: "otpauth://totp/x" };
    mocks.enrollTotp.mockResolvedValue(enroll);

    const mutation = enrollTotpMutation();
    const result = await mutation.mutationFn();

    expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    expect(result).toBe(enroll);
  });
});

describe("confirmEnrollMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to the facade mfa.confirmEnroll with the secret + code", async () => {
    const confirmed = { succeeded: true, backupCodes: ["a", "b"] };
    mocks.confirmEnroll.mockResolvedValue(confirmed);

    const mutation = confirmEnrollMutation(fakeQueryClient());
    const result = await mutation.mutationFn({ secret: "ABC", code: "123456" });

    expect(mocks.confirmEnroll).toHaveBeenCalledWith("ABC", "123456");
    expect(result).toBe(confirmed);
  });

  it("invalidates the ['mfa', 'status'] query on success", () => {
    const queryClient = fakeQueryClient();

    confirmEnrollMutation(queryClient).onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["mfa", "status"] });
  });
});

describe("disableMfaMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to the facade mfa.disable with the password", async () => {
    const disabled = { succeeded: true };
    mocks.disable.mockResolvedValue(disabled);

    const mutation = disableMfaMutation(fakeQueryClient());
    const result = await mutation.mutationFn("hunter2");

    expect(mocks.disable).toHaveBeenCalledWith("hunter2");
    expect(result).toBe(disabled);
  });

  it("invalidates the ['mfa', 'status'] query on success", () => {
    const queryClient = fakeQueryClient();

    disableMfaMutation(queryClient).onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["mfa", "status"] });
  });
});

describe("regenerateBackupCodesMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mutationFn delegates to the facade mfa.regenerateBackupCodes with the password", async () => {
    const regenerated = { codes: ["x", "y", "z"] };
    mocks.regenerateBackupCodes.mockResolvedValue(regenerated);

    const mutation = regenerateBackupCodesMutation(fakeQueryClient());
    const result = await mutation.mutationFn("hunter2");

    expect(mocks.regenerateBackupCodes).toHaveBeenCalledWith("hunter2");
    expect(result).toBe(regenerated);
  });

  it("invalidates the ['mfa', 'status'] query on success", () => {
    const queryClient = fakeQueryClient();

    regenerateBackupCodesMutation(queryClient).onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["mfa", "status"] });
  });
});
