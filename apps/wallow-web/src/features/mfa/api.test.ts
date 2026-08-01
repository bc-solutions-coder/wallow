import { describe, expect, it } from "vitest";

/**
 * The MFA feature's `api.ts` re-export seam over `@bc-solutions-coder/sdk/query`.
 *
 * MFA sweeps by OPERATION, not by tag: hey-api tags every MFA operation
 * `Identity`, a tag it also puts on the account and session operations, so
 * `queriesWithTag("Identity")` would refetch most of the identity module to
 * repaint one status card. That distinction is invisible at a call site.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "@bc-solutions-coder/testing/invalidation";
import * as api from "./api";

const statusKey: readonly unknown[] = api.mfaGetStatusQueryKey();

describe("api.ts re-exports the SDK mfa query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.mfaGetStatusOptions).toBe(query.mfaGetStatusOptions);
    expect(api.mfaGetStatusQueryKey).toBe(query.mfaGetStatusQueryKey);
    expect(api.mfaEnrollTotpMutation).toBe(query.mfaEnrollTotpMutation);
    expect(api.mfaConfirmEnrollmentMutation).toBe(query.mfaConfirmEnrollmentMutation);
    expect(api.mfaDisableMutation).toBe(query.mfaDisableMutation);
    expect(api.mfaRegenerateBackupCodesMutation).toBe(query.mfaRegenerateBackupCodesMutation);
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
    expect(api.queriesWithTag).toBe(query.queriesWithTag);
  });
});

describe("mfa invalidation", () => {
  it("reaches the status card by operation", () => {
    expect(sweeps(api.queriesForOperation(statusKey), statusKey)).toBe(true);
  });

  it("does not reach the session list, which shares MFA's Identity tag", () => {
    expect(sweeps(api.queriesForOperation(statusKey), query.sessionListSessionsQueryKey())).toBe(
      false,
    );
    // The tag alone would have: this is why MFA sweeps by operation.
    expect(sweeps(api.queriesWithTag("Identity"), query.sessionListSessionsQueryKey())).toBe(true);
  });
});
