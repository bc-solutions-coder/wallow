import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import { appsQueries, registerAppMutation, upsertBrandingMutation } from "./apps";
import { queryKeys } from "./keys";

describe("appsQueries", () => {
  it("keys every option from the central factory", () => {
    expect(appsQueries.list().queryKey).toEqual(queryKeys.apps.all);
    expect(appsQueries.detail("c1").queryKey).toEqual(queryKeys.apps.detail("c1"));
  });

  it("invalidates the factory keys on mutation success", async () => {
    const queryClient = new QueryClient();
    const invalidated: unknown[] = [];
    queryClient.invalidateQueries = (filters?: { queryKey?: unknown }) => {
      invalidated.push(filters?.queryKey);
      return Promise.resolve();
    };
    registerAppMutation(queryClient).onSuccess();
    upsertBrandingMutation(queryClient, "c1").onSuccess();
    expect(invalidated).toEqual([queryKeys.apps.all, queryKeys.apps.detail("c1")]);
  });
});
