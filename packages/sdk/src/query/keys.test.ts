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
    expect(queryKeys.auth.consentInfo("c1", [])).toEqual(["auth", "consent", "c1"]);
    expect(queryKeys.auth.invitation("t")).toEqual(["auth", "invitation", "t"]);
    expect(queryKeys.auth.verifyEmail("e@x.dev", "t")).toEqual([
      "auth",
      "verify-email",
      "e@x.dev",
      "t",
    ]);
    expect(queryKeys.auth.redirectValidation("/u")).toEqual(["auth", "redirect-validation", "/u"]);
  });

  it("folds the requested scopes into the consent key so scope sets cannot collide", () => {
    // The consent prompt's CONTENT is a function of (clientId, scopes) — the same
    // client asking for a wider set is a different question to put to the user.
    // A key that ignored the scopes would serve the first answer to the second
    // question out of the cache.
    //
    // The scopes are folded as ONE space-joined segment, the same delimiter the
    // wire uses (OAuth's own convention, and what the consent-info endpoint
    // splits on), so the key reads as the request it stands for.
    expect(queryKeys.auth.consentInfo("c1", ["openid", "profile"])).toEqual([
      "auth",
      "consent",
      "c1",
      "openid profile",
    ]);

    expect(queryKeys.auth.consentInfo("c1", ["openid"])).not.toEqual(
      queryKeys.auth.consentInfo("c1", ["openid", "profile"]),
    );

    // The scope-less key stays a PREFIX of the scoped ones, so invalidating
    // `consentInfo(clientId)` still sweeps every scope set for that client.
    const bare = queryKeys.auth.consentInfo("c1");
    expect(queryKeys.auth.consentInfo("c1", ["openid"]).slice(0, bare.length)).toEqual([...bare]);
  });

  it("folds the client id into the redirect-validation key so clients cannot share an answer", () => {
    // "Is this URI allowed?" is a question about (uri, client): the API scopes it
    // to ONE client's registered origins when a client id is supplied
    // (OpenIddictRedirectUriValidator.IsAllowedAsync). A key that ignored the id
    // would serve client A's "yes" to client B asking about the same URI — the
    // allow-list bypass this key exists to prevent, cached.
    expect(queryKeys.auth.redirectValidation("/u", "client-a")).toEqual([
      "auth",
      "redirect-validation",
      "/u",
      "client-a",
    ]);

    expect(queryKeys.auth.redirectValidation("/u", "client-a")).not.toEqual(
      queryKeys.auth.redirectValidation("/u", "client-b"),
    );

    // The unscoped answer is a DIFFERENT question (the union of every client's
    // origins), so it must not collide with any scoped one either.
    expect(queryKeys.auth.redirectValidation("/u")).not.toEqual(
      queryKeys.auth.redirectValidation("/u", "client-a"),
    );

    // Appended, not substituted: the client-less key stays a PREFIX, so
    // invalidating `redirectValidation(url)` still sweeps every client's answer
    // for that url — the rule the consent key already follows.
    const bare = queryKeys.auth.redirectValidation("/u");
    expect(queryKeys.auth.redirectValidation("/u", "client-a").slice(0, bare.length)).toEqual([
      ...bare,
    ]);
  });

  it("builds child keys from parent keys so hierarchy cannot drift", () => {
    const detail = queryKeys.organizations.detail("o1");
    expect(queryKeys.organizations.members("o1").slice(0, detail.length)).toEqual([...detail]);
  });
});
