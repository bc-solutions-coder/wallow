import { describe, expect, it } from "vitest";

/**
 * MFA feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Everything behind it is GENERATED as of
 * Wallow-pu6a.5.5, so the invalidation model (confirm/disable/regenerate sweep
 * the status card; `enrollTotp` mints a one-time secret and sweeps nothing) moved
 * to the call sites, where the component specs assert it against the card.
 *
 * The decision this seam still owns — and the reason this spec exists — is that
 * MFA sweeps by OPERATION and not by tag. hey-api tags every MFA operation
 * `Identity`, a tag it also puts on the account and session operations, so
 * `queriesWithTag("Identity")` would refetch most of the identity module to
 * repaint one status card. That distinction is invisible at a call site and is
 * asserted here.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "../../test/invalidation";
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
