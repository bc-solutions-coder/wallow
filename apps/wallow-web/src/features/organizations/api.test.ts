import { describe, expect, it } from "vitest";

/**
 * Organizations feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Everything behind it is GENERATED as of
 * Wallow-pu6a.5.5, so the per-mutation `onSuccess` sweeps this spec used to pin
 * moved to the call sites, where the component specs assert them against the
 * rendered screen.
 *
 * Two seam-level decisions survive here, both invisible from a call site:
 *  - the `Organizations` tag covers the org list, detail and members together,
 *    which is what makes it the right sweep after create/archive/reactivate;
 *  - an org's CLIENTS are NOT in it. `clientsGetByTenant` is tagged `Clients`,
 *    so registering a client sweeps by that tag (or by operation) — reaching for
 *    `Organizations` there would refetch everything except the list that changed.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "../../test/invalidation";
import * as api from "./api";

const membersKey: readonly unknown[] = api.organizationsGetMembersQueryKey({ path: { id: "o1" } });
const clientsKey: readonly unknown[] = api.clientsGetByTenantQueryKey({
  path: { tenantId: "o1" },
});

describe("api.ts re-exports the SDK organizations query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.organizationsGetAllOptions).toBe(query.organizationsGetAllOptions);
    expect(api.organizationsGetAllQueryKey).toBe(query.organizationsGetAllQueryKey);
    expect(api.organizationsGetByIdOptions).toBe(query.organizationsGetByIdOptions);
    expect(api.organizationsGetMembersOptions).toBe(query.organizationsGetMembersOptions);
    expect(api.organizationsGetMembersQueryKey).toBe(query.organizationsGetMembersQueryKey);
    expect(api.organizationsCreateMutation).toBe(query.organizationsCreateMutation);
    expect(api.organizationsAddMemberMutation).toBe(query.organizationsAddMemberMutation);
    expect(api.organizationsRemoveMemberMutation).toBe(query.organizationsRemoveMemberMutation);
    expect(api.organizationsArchiveMutation).toBe(query.organizationsArchiveMutation);
    expect(api.organizationsReactivateMutation).toBe(query.organizationsReactivateMutation);
    expect(api.clientsCreateMutation).toBe(query.clientsCreateMutation);
    expect(api.clientsGetByTenantOptions).toBe(query.clientsGetByTenantOptions);
    expect(api.clientsGetByTenantQueryKey).toBe(query.clientsGetByTenantQueryKey);
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
    expect(api.queriesWithTag).toBe(query.queriesWithTag);
  });
});

describe("organizations invalidation", () => {
  it("sweeps the org list and an org's members under one tag", () => {
    expect(sweeps(api.queriesWithTag("Organizations"), api.organizationsGetAllQueryKey())).toBe(
      true,
    );
    expect(sweeps(api.queriesWithTag("Organizations"), membersKey)).toBe(true);
  });

  it("does NOT reach an org's clients, which are tagged separately", () => {
    expect(sweeps(api.queriesWithTag("Organizations"), clientsKey)).toBe(false);
    expect(sweeps(api.queriesWithTag("Clients"), clientsKey)).toBe(true);
  });

  it("narrows to a single operation when only the members changed", () => {
    expect(sweeps(api.queriesForOperation(membersKey), membersKey)).toBe(true);
    expect(sweeps(api.queriesForOperation(membersKey), api.organizationsGetAllQueryKey())).toBe(
      false,
    );
  });
});
