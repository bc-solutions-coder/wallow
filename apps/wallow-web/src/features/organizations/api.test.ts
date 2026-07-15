import { beforeEach, describe, expect, it, vi } from "vitest";
import type { QueryClient } from "@tanstack/react-query";

/**
 * Organizations feature query layer (Wallow-8w1h.4.1) — spec for the CANONICAL
 * feature-folder `api.ts` that Phases 4-6 copy. The `getWallowSdk()` facade is
 * mocked: these tests assert the query/mutation layer's KEY STABILITY and its
 * DELEGATION to the facade slice, not the wire.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  list: vi.fn(),
  get: vi.fn(),
  create: vi.fn(),
  members: vi.fn(),
  addMember: vi.fn(),
  removeMember: vi.fn(),
  archive: vi.fn(),
  reactivate: vi.fn(),
}));

// Route/component files import only from this feature's api.ts; api.ts in turn
// imports getWallowSdk. We mock the facade module so the slice methods are spies.
vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: {
      list: mocks.list,
      get: mocks.get,
      create: mocks.create,
      members: mocks.members,
      addMember: mocks.addMember,
      removeMember: mocks.removeMember,
      archive: mocks.archive,
      reactivate: mocks.reactivate,
    },
  }),
}));

import {
  addMemberMutation,
  archiveOrganizationMutation,
  createOrganizationMutation,
  organizationsQueries,
  reactivateOrganizationMutation,
  removeMemberMutation,
} from "./api";

/** Invoke a queryOptions `queryFn` while ignoring its QueryFunctionContext arg. */
async function callQueryFn(queryFn: unknown): Promise<unknown> {
  return (queryFn as () => Promise<unknown>)();
}

describe("organizationsQueries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("list", () => {
    it("keys the list query as ['orgs']", () => {
      expect(organizationsQueries.list().queryKey).toEqual(["orgs"]);
    });

    it("keeps the list queryKey stable across calls", () => {
      expect(organizationsQueries.list().queryKey).toEqual(organizationsQueries.list().queryKey);
    });

    it("queryFn delegates to the facade organizations.list and returns its data", async () => {
      const orgs = [{ id: "o1", name: "Acme" }];
      mocks.list.mockResolvedValue(orgs);

      const result = await callQueryFn(organizationsQueries.list().queryFn);

      expect(mocks.list).toHaveBeenCalledTimes(1);
      expect(result).toBe(orgs);
    });
  });

  describe("detail", () => {
    it("keys the detail query as ['orgs', id]", () => {
      expect(organizationsQueries.detail("o1").queryKey).toEqual(["orgs", "o1"]);
    });

    it("queryFn delegates to the facade organizations.get with the id", async () => {
      const org = { id: "o1", name: "Acme" };
      mocks.get.mockResolvedValue(org);

      const result = await callQueryFn(organizationsQueries.detail("o1").queryFn);

      expect(mocks.get).toHaveBeenCalledWith("o1");
      expect(result).toBe(org);
    });
  });

  describe("members (Wallow-8w1h.4.4)", () => {
    it("keys the members query as ['orgs', id, 'members']", () => {
      expect(organizationsQueries.members("o1").queryKey).toEqual(["orgs", "o1", "members"]);
    });

    it("queryFn delegates to the facade organizations.members with the id", async () => {
      const members = [{ id: "u1", email: "a@b.c", roles: ["Owner"] }];
      mocks.members.mockResolvedValue(members);

      const result = await callQueryFn(organizationsQueries.members("o1").queryFn);

      expect(mocks.members).toHaveBeenCalledWith("o1");
      expect(result).toBe(members);
    });
  });
});

describe("member & lifecycle mutations (Wallow-8w1h.4.4)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function fakeQueryClient(): QueryClient {
    return { invalidateQueries: vi.fn() } as unknown as QueryClient;
  }

  describe("addMemberMutation", () => {
    it("mutationFn delegates to organizations.addMember with the org id and body", async () => {
      const added = { id: "u2" };
      mocks.addMember.mockResolvedValue(added);

      const mutation = addMemberMutation(fakeQueryClient(), "o1");
      const result = await mutation.mutationFn({ userId: "u2" });

      expect(mocks.addMember).toHaveBeenCalledWith("o1", { userId: "u2" });
      expect(result).toBe(added);
    });

    it("invalidates the ['orgs', id, 'members'] query on success", () => {
      const queryClient = fakeQueryClient();

      addMemberMutation(queryClient, "o1").onSuccess();

      expect(queryClient.invalidateQueries).toHaveBeenCalledWith({
        queryKey: ["orgs", "o1", "members"],
      });
    });
  });

  describe("removeMemberMutation", () => {
    it("mutationFn delegates to organizations.removeMember with the org id and user id", async () => {
      mocks.removeMember.mockResolvedValue(undefined);

      const mutation = removeMemberMutation(fakeQueryClient(), "o1");
      await mutation.mutationFn("u2");

      expect(mocks.removeMember).toHaveBeenCalledWith("o1", "u2");
    });

    it("invalidates the ['orgs', id, 'members'] query on success", () => {
      const queryClient = fakeQueryClient();

      removeMemberMutation(queryClient, "o1").onSuccess();

      expect(queryClient.invalidateQueries).toHaveBeenCalledWith({
        queryKey: ["orgs", "o1", "members"],
      });
    });
  });

  describe("archiveOrganizationMutation", () => {
    it("mutationFn delegates to organizations.archive with the org id", async () => {
      mocks.archive.mockResolvedValue(undefined);

      const mutation = archiveOrganizationMutation(fakeQueryClient(), "o1");
      await mutation.mutationFn();

      expect(mocks.archive).toHaveBeenCalledWith("o1");
    });

    it("invalidates the ['orgs'] list query on success", () => {
      const queryClient = fakeQueryClient();

      archiveOrganizationMutation(queryClient, "o1").onSuccess();

      expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["orgs"] });
    });
  });

  describe("reactivateOrganizationMutation", () => {
    it("mutationFn delegates to organizations.reactivate with the org id", async () => {
      mocks.reactivate.mockResolvedValue(undefined);

      const mutation = reactivateOrganizationMutation(fakeQueryClient(), "o1");
      await mutation.mutationFn();

      expect(mocks.reactivate).toHaveBeenCalledWith("o1");
    });

    it("invalidates the ['orgs'] list query on success", () => {
      const queryClient = fakeQueryClient();

      reactivateOrganizationMutation(queryClient, "o1").onSuccess();

      expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["orgs"] });
    });
  });
});

describe("createOrganizationMutation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function fakeQueryClient(): QueryClient {
    return { invalidateQueries: vi.fn() } as unknown as QueryClient;
  }

  it("mutationFn delegates to the facade organizations.create with the body", async () => {
    const created = { id: "o2", name: "New" };
    mocks.create.mockResolvedValue(created);
    const body = { name: "New", domain: null };

    const mutation = createOrganizationMutation(fakeQueryClient());
    const result = await mutation.mutationFn(body);

    expect(mocks.create).toHaveBeenCalledWith(body);
    expect(result).toBe(created);
  });

  it("invalidates the ['orgs'] list query on success", () => {
    const queryClient = fakeQueryClient();

    const mutation = createOrganizationMutation(queryClient);
    mutation.onSuccess();

    expect(queryClient.invalidateQueries).toHaveBeenCalledWith({ queryKey: ["orgs"] });
  });
});
