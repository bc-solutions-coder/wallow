import { describe, expect, it } from "vitest";

/**
 * Organizations feature `api.ts` — a thin re-export seam over
 * `@bc-solutions-coder/sdk/query`. Everything behind it is generated, so what
 * is testable here is the seam itself: identity of the re-exports, and which
 * queries each invalidation predicate reaches. The per-mutation sweeps live at
 * the call sites, where the component specs assert them against the screen.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "@bc-solutions-coder/testing/invalidation";
import * as api from "./api";

const membersKey: readonly unknown[] = api.organizationsGetMembersQueryKey({ path: { id: "o1" } });
const clientsKey: readonly unknown[] = api.organizationClientsListQueryKey({
  path: { orgId: "o1" },
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
    expect(api.organizationClientsListOptions).toBe(query.organizationClientsListOptions);
    expect(api.organizationClientsListQueryKey).toBe(query.organizationClientsListQueryKey);
    expect(api.organizationClientsRegisterMutation).toBe(query.organizationClientsRegisterMutation);
    expect(api.organizationClientsSuspendMutation).toBe(query.organizationClientsSuspendMutation);
    expect(api.organizationClientsReinstateMutation).toBe(
      query.organizationClientsReinstateMutation,
    );
    expect(api.organizationClientsDeleteMutation).toBe(query.organizationClientsDeleteMutation);
    expect(api.organizationsPlacePlatformSuspensionMutation).toBe(
      query.organizationsPlacePlatformSuspensionMutation,
    );
    expect(api.organizationsLiftPlatformSuspensionMutation).toBe(
      query.organizationsLiftPlatformSuspensionMutation,
    );
    expect(api.organizationClientsPlacePlatformSuspensionMutation).toBe(
      query.organizationClientsPlacePlatformSuspensionMutation,
    );
    expect(api.organizationClientsLiftPlatformSuspensionMutation).toBe(
      query.organizationClientsLiftPlatformSuspensionMutation,
    );
    expect(api.scopesListOptions).toBe(query.scopesListOptions);
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
    expect(sweeps(api.queriesWithTag("Organization Clients"), clientsKey)).toBe(true);
  });

  it("narrows to a single operation when only the members changed", () => {
    expect(sweeps(api.queriesForOperation(membersKey), membersKey)).toBe(true);
    expect(sweeps(api.queriesForOperation(membersKey), api.organizationsGetAllQueryKey())).toBe(
      false,
    );
  });
});
