import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

/**
 * MFA feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. This spec pins the seam (re-export identity)
 * and the preserved key/invalidation model: confirm/disable/regenerate each
 * sweep the status card key; `enrollTotp` mints a one-time secret and does NOT
 * invalidate (no onSuccess). The old `vi.mock("../../lib/wallow-sdk")`
 * delegation spec is gone.
 */

import * as api from "./api";
import { mfaQueries } from "./api";
import { queryKeys } from "@bc-solutions-coder/sdk/query";
import * as query from "@bc-solutions-coder/sdk/query";

/** A QueryClient whose invalidateQueries records the keys it was asked to sweep. */
function captureInvalidations(): { client: QueryClient; keys: unknown[] } {
  const client = new QueryClient();
  const keys: unknown[] = [];
  client.invalidateQueries = (filters?: { queryKey?: unknown }) => {
    keys.push(filters?.queryKey);
    return Promise.resolve();
  };
  return { client, keys };
}

describe("api.ts re-exports the SDK mfa query layer", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.mfaQueries).toBe(query.mfaQueries);
    expect(api.enrollTotpMutation).toBe(query.enrollTotpMutation);
    expect(api.confirmEnrollMutation).toBe(query.confirmEnrollMutation);
    expect(api.disableMfaMutation).toBe(query.disableMfaMutation);
    expect(api.regenerateBackupCodesMutation).toBe(query.regenerateBackupCodesMutation);
  });
});

describe("mfaQueries", () => {
  it("keys the status query from the central queryKeys factory", () => {
    expect(mfaQueries.status().queryKey).toEqual(queryKeys.mfa.status());
  });

  it("keeps the status queryKey stable across calls", () => {
    expect(mfaQueries.status().queryKey).toEqual(mfaQueries.status().queryKey);
  });
});

describe("mfa mutation invalidation", () => {
  it("enrollTotpMutation has no onSuccess (one-time secret, status unchanged)", () => {
    expect(api.enrollTotpMutation()).not.toHaveProperty("onSuccess");
  });

  it("confirmEnrollMutation sweeps the status card", () => {
    const { client, keys } = captureInvalidations();
    api.confirmEnrollMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.mfa.status()]);
  });

  it("disableMfaMutation sweeps the status card", () => {
    const { client, keys } = captureInvalidations();
    api.disableMfaMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.mfa.status()]);
  });

  it("regenerateBackupCodesMutation sweeps the status card", () => {
    const { client, keys } = captureInvalidations();
    api.regenerateBackupCodesMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.mfa.status()]);
  });
});
