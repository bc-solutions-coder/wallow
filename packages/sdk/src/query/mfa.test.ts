import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import { queryKeys } from "./keys";
import {
  confirmEnrollMutation,
  disableMfaMutation,
  mfaQueries,
  regenerateBackupCodesMutation,
} from "./mfa";

describe("mfaQueries", () => {
  it("keys every option from the central factory", () => {
    expect(mfaQueries.status().queryKey).toEqual(queryKeys.mfa.status());
  });

  it("invalidates the factory keys on mutation success", async () => {
    const queryClient = new QueryClient();
    const invalidated: unknown[] = [];
    queryClient.invalidateQueries = (filters?: { queryKey?: unknown }) => {
      invalidated.push(filters?.queryKey);
      return Promise.resolve();
    };
    // enrollTotp mints a one-time secret and does NOT invalidate; confirm,
    // disable, and regenerate all change the status card and sweep its key.
    confirmEnrollMutation(queryClient).onSuccess();
    disableMfaMutation(queryClient).onSuccess();
    regenerateBackupCodesMutation(queryClient).onSuccess();
    expect(invalidated).toEqual([
      queryKeys.mfa.status(),
      queryKeys.mfa.status(),
      queryKeys.mfa.status(),
    ]);
  });
});
