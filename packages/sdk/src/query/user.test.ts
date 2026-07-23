import { describe, expect, it } from "vitest";

import { queryKeys } from "./keys";
import { userQueries } from "./user";

describe("userQueries", () => {
  it("keys the current-user query off the shared auth factory", () => {
    expect(userQueries.currentUser().queryKey).toEqual(queryKeys.auth.currentUser());
  });

  it("holds the current user for 30s so beforeLoad stops refetching per navigation", () => {
    expect(userQueries.currentUser().staleTime).toBe(30_000);
  });
});
