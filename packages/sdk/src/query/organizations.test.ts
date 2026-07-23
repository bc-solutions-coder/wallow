import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import { queryKeys } from "./keys";
import {
  addMemberMutation,
  createOrganizationMutation,
  organizationsQueries,
  registerClientMutation,
} from "./organizations";

describe("organizationsQueries", () => {
  it("keys every option from the central factory", () => {
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

  it("invalidates the factory keys on mutation success", async () => {
    const queryClient = new QueryClient();
    const invalidated: unknown[] = [];
    queryClient.invalidateQueries = (filters?: { queryKey?: unknown }) => {
      invalidated.push(filters?.queryKey);
      return Promise.resolve();
    };
    createOrganizationMutation(queryClient).onSuccess();
    addMemberMutation(queryClient, "o1").onSuccess();
    registerClientMutation(queryClient, "o1").onSuccess();
    expect(invalidated).toEqual([
      queryKeys.organizations.all,
      queryKeys.organizations.members("o1"),
      queryKeys.organizations.clients("o1"),
    ]);
  });
});
