import { describe, expect, it } from "vitest";

import { authQueries } from "./auth";
import { queryKeys } from "./keys";

describe("authQueries", () => {
  it("keys every option from the central factory", () => {
    // wallow-auth's flow queries are read-only (its mutations end in navigation,
    // not cache updates), so this module ships queries only — no invalidation
    // test. currentUser is intentionally absent here: it lives in user.ts.
    expect(authQueries.externalProviders().queryKey).toEqual(queryKeys.auth.externalProviders());
    expect(authQueries.clientTenant("c1").queryKey).toEqual(queryKeys.auth.clientTenant("c1"));
    expect(authQueries.consentInfo("c1").queryKey).toEqual(queryKeys.auth.consentInfo("c1"));
    expect(authQueries.invitation("t1").queryKey).toEqual(queryKeys.auth.invitation("t1"));
    expect(authQueries.verifyEmail("e@x.dev", "t1").queryKey).toEqual(
      queryKeys.auth.verifyEmail("e@x.dev", "t1"),
    );
    expect(authQueries.redirectValidation("https://x.dev").queryKey).toEqual(
      queryKeys.auth.redirectValidation("https://x.dev"),
    );
  });
});
