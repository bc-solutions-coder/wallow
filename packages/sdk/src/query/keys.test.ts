import { describe, expect, it } from "vitest";

import { queryKeys } from "./keys";

describe("queryKeys", () => {
  it("keeps the existing wallow-web key hierarchy so caches sweep by prefix", () => {
    expect(queryKeys.organizations.all).toEqual(["orgs"]);
    expect(queryKeys.organizations.detail("o1")).toEqual(["orgs", "o1"]);
    expect(queryKeys.organizations.members("o1")).toEqual(["orgs", "o1", "members"]);
    expect(queryKeys.organizations.clients("o1")).toEqual(["orgs", "o1", "clients"]);
    expect(queryKeys.apps.all).toEqual(["apps"]);
    expect(queryKeys.apps.detail("c1")).toEqual(["apps", "c1"]);
    expect(queryKeys.settings.profile()).toEqual(["settings", "profile"]);
    expect(queryKeys.mfa.status()).toEqual(["mfa", "status"]);
    expect(queryKeys.inquiries.all).toEqual(["inquiries"]);
    expect(queryKeys.inquiries.detail("i1")).toEqual(["inquiries", "i1"]);
    expect(queryKeys.inquiries.comments("i1")).toEqual(["inquiries", "i1", "comments"]);
  });

  it("namespaces every auth-flow key under ['auth'] (fixes the external-providers collision)", () => {
    expect(queryKeys.auth.currentUser()).toEqual(["auth", "current-user"]);
    expect(queryKeys.auth.externalProviders()).toEqual(["auth", "external-providers"]);
    expect(queryKeys.auth.clientTenant("c1")).toEqual(["auth", "client-tenant", "c1"]);
    expect(queryKeys.auth.consentInfo("c1")).toEqual(["auth", "consent", "c1"]);
    expect(queryKeys.auth.invitation("t")).toEqual(["auth", "invitation", "t"]);
    expect(queryKeys.auth.verifyEmail("e@x.dev", "t")).toEqual([
      "auth",
      "verify-email",
      "e@x.dev",
      "t",
    ]);
    expect(queryKeys.auth.redirectValidation("/u")).toEqual(["auth", "redirect-validation", "/u"]);
  });

  it("builds child keys from parent keys so hierarchy cannot drift", () => {
    const detail = queryKeys.organizations.detail("o1");
    expect(queryKeys.organizations.members("o1").slice(0, detail.length)).toEqual([...detail]);
  });
});
