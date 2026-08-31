import { describe, expect, it } from "vitest";

/**
 * Settings feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`: the current-user read plus the
 * connected-applications list/withdraw pair.
 *
 * Re-export by IDENTITY is the point: the profile screen and the dashboard's
 * auth guard must read the same generated operation, or a wrapper key gives
 * one resource two cache entries and two requests.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

describe("api.ts re-exports the SDK settings query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.usersGetCurrentUserOptions).toBe(query.usersGetCurrentUserOptions);
    expect(api.usersGetCurrentUserQueryKey).toBe(query.usersGetCurrentUserQueryKey);
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
    expect(api.meAuthorizationsListConnectedApplicationsOptions).toBe(
      query.meAuthorizationsListConnectedApplicationsOptions,
    );
    expect(api.meAuthorizationsListConnectedApplicationsQueryKey).toBe(
      query.meAuthorizationsListConnectedApplicationsQueryKey,
    );
    expect(api.meAuthorizationsWithdrawConsentMutation).toBe(
      query.meAuthorizationsWithdrawConsentMutation,
    );
  });
});

describe("the profile read", () => {
  it("shares one cache entry with every other current-user read", () => {
    expect(api.usersGetCurrentUserQueryKey()).toEqual(query.usersGetCurrentUserQueryKey());
  });
});
