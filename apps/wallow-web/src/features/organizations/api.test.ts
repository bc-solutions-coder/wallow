import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

/**
 * Organizations feature `api.ts` (Wallow-evd5.2.2) — after the SDK-query-layer
 * adoption, `api.ts` is a THIN RE-EXPORT SEAM over `@bc-solutions-coder/sdk/query`,
 * not a hand-rolled query layer. Route/component files still import from
 * `./api`, so this spec pins the seam:
 *  - every symbol `./api` exposes IS the SDK export (re-export identity), and
 *  - the query keys + mutation invalidations still resolve to the central
 *    `queryKeys` factory (behavior preserved across the swap).
 * The old `vi.mock("../../lib/wallow-sdk")` delegation spec is gone: the
 * re-exported factories no longer call the facade, so mocking it asserts nothing.
 */

import * as api from "./api";
import { organizationsQueries } from "./api";
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

describe("api.ts re-exports the SDK organizations query layer", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.organizationsQueries).toBe(query.organizationsQueries);
    expect(api.createOrganizationMutation).toBe(query.createOrganizationMutation);
    expect(api.addMemberMutation).toBe(query.addMemberMutation);
    expect(api.removeMemberMutation).toBe(query.removeMemberMutation);
    expect(api.archiveOrganizationMutation).toBe(query.archiveOrganizationMutation);
    expect(api.reactivateOrganizationMutation).toBe(query.reactivateOrganizationMutation);
    expect(api.registerClientMutation).toBe(query.registerClientMutation);
  });
});

describe("organizationsQueries", () => {
  it("keys every option from the central queryKeys factory", () => {
    expect(organizationsQueries.list().queryKey).toEqual(queryKeys.organizations.all);
    expect(organizationsQueries.detail("o1").queryKey).toEqual(
      queryKeys.organizations.detail("o1"),
    );
    expect(organizationsQueries.members("o1").queryKey).toEqual(
      queryKeys.organizations.members("o1"),
    );
    expect(organizationsQueries.clients("o1").queryKey).toEqual(
      queryKeys.organizations.clients("o1"),
    );
  });

  it("keeps the list queryKey stable across calls", () => {
    expect(organizationsQueries.list().queryKey).toEqual(organizationsQueries.list().queryKey);
  });
});

describe("organizations mutation invalidation", () => {
  it("createOrganizationMutation sweeps the organizations list", () => {
    const { client, keys } = captureInvalidations();
    api.createOrganizationMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.organizations.all]);
  });

  it("addMemberMutation sweeps that org's members", () => {
    const { client, keys } = captureInvalidations();
    api.addMemberMutation(client, "o1").onSuccess();
    expect(keys).toEqual([queryKeys.organizations.members("o1")]);
  });

  it("removeMemberMutation sweeps that org's members", () => {
    const { client, keys } = captureInvalidations();
    api.removeMemberMutation(client, "o1").onSuccess();
    expect(keys).toEqual([queryKeys.organizations.members("o1")]);
  });

  it("archiveOrganizationMutation sweeps the organizations list", () => {
    const { client, keys } = captureInvalidations();
    api.archiveOrganizationMutation(client, "o1").onSuccess();
    expect(keys).toEqual([queryKeys.organizations.all]);
  });

  it("reactivateOrganizationMutation sweeps the organizations list", () => {
    const { client, keys } = captureInvalidations();
    api.reactivateOrganizationMutation(client, "o1").onSuccess();
    expect(keys).toEqual([queryKeys.organizations.all]);
  });

  it("registerClientMutation sweeps that org's clients", () => {
    const { client, keys } = captureInvalidations();
    api.registerClientMutation(client, "o1").onSuccess();
    expect(keys).toEqual([queryKeys.organizations.clients("o1")]);
  });
});
