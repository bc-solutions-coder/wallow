import { beforeEach, describe, expect, it, vi } from "vitest";

import { authQueries } from "./auth";
import { queryKeys } from "./keys";

// Hoisted so the vi.mock factory and the test bodies share the same spy. The
// consent query is the one factory here whose ARGUMENTS matter (the scopes the
// relying party asked for), so this file mocks the seam below it rather than
// asserting on keys alone.
const mocks = vi.hoisted(() => ({
  getConsentInfo: vi.fn(),
  validateRedirectUri: vi.fn(),
}));

vi.mock("./bootstrap", () => ({
  ensureQueryBootstrapped: () => {},
}));

vi.mock("../auth-client", () => ({
  createAuthClient: () => ({
    getConsentInfo: mocks.getConsentInfo,
    validateRedirectUri: mocks.validateRedirectUri,
  }),
}));

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

/**
 * The consent query is the only one in this module that takes an argument the
 * SERVER's answer depends on beyond an id: the scopes being requested. The
 * consent screen exists to show the user that list, so this factory has to be
 * able to carry it — both into the request and into the cache key.
 */
describe("authQueries.consentInfo scopes", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("keys the scoped query off the central factory, scopes included", () => {
    expect(authQueries.consentInfo("c1", ["openid", "profile"]).queryKey).toEqual(
      queryKeys.auth.consentInfo("c1", ["openid", "profile"]),
    );

    // Different scope sets for the SAME client must not share a cache entry —
    // the answer to "may this app read your profile" is not the answer to "may
    // it read your storage".
    expect(authQueries.consentInfo("c1", ["openid"]).queryKey).not.toEqual(
      authQueries.consentInfo("c1", ["openid", "storage.read"]).queryKey,
    );
  });

  it("forwards the requested scopes to the auth client", async () => {
    mocks.getConsentInfo.mockResolvedValue({ clientId: "c1", requestedScopes: [] });

    const options = authQueries.consentInfo("c1", ["openid", "profile"]);
    await (options.queryFn as () => Promise<unknown>)();

    expect(mocks.getConsentInfo).toHaveBeenCalledWith("c1", ["openid", "profile"]);
  });

  it("still asks with no scopes when none were requested", async () => {
    mocks.getConsentInfo.mockResolvedValue({ clientId: "c1", requestedScopes: [] });

    const options = authQueries.consentInfo("c1");
    await (options.queryFn as () => Promise<unknown>)();

    const scopes: unknown = mocks.getConsentInfo.mock.calls[0]?.[1];

    expect(mocks.getConsentInfo.mock.calls[0]?.[0]).toBe("c1");
    expect(scopes === undefined || (Array.isArray(scopes) && scopes.length === 0)).toBe(true);
  });
});

/**
 * THE SCOPED ALLOW-LIST PROBE (Wallow-nv7l.1, closing Wallow-53kr).
 *
 * `/redirect-uri/validate` takes a `clientId` and answers against THAT client's
 * registered origins; without one it answers against the union of every
 * client's. The MFA-challenge screen asks it whether the external-login
 * hand-off's absolute returnUrl is allowed, and today asks unscoped — so a URI
 * registered by any client at all passes the check for every client. The factory
 * has to be able to carry the id into both the request and the cache key.
 */
describe("authQueries.redirectValidation client scoping", () => {
  const URL_UNDER_TEST: string = "https://app.example.com/callback";

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("keys the scoped query off the central factory, client included", () => {
    expect(authQueries.redirectValidation(URL_UNDER_TEST, "client-a").queryKey).toEqual(
      queryKeys.auth.redirectValidation(URL_UNDER_TEST, "client-a"),
    );

    // The same URI asked about by two clients is two questions with two answers.
    // Sharing a cache entry would let one client's registration answer for
    // another's — the bypass the scoping exists to close, served from memory.
    expect(authQueries.redirectValidation(URL_UNDER_TEST, "client-a").queryKey).not.toEqual(
      authQueries.redirectValidation(URL_UNDER_TEST, "client-b").queryKey,
    );
    expect(authQueries.redirectValidation(URL_UNDER_TEST).queryKey).not.toEqual(
      authQueries.redirectValidation(URL_UNDER_TEST, "client-a").queryKey,
    );
  });

  it("forwards the client id to the auth client", async () => {
    mocks.validateRedirectUri.mockResolvedValue({ allowed: true });

    const options = authQueries.redirectValidation(URL_UNDER_TEST, "client-a");
    await (options.queryFn as () => Promise<unknown>)();

    expect(mocks.validateRedirectUri).toHaveBeenCalledWith(URL_UNDER_TEST, "client-a");
  });

  it("still asks unscoped when the caller has no client id", async () => {
    // The password path and the logout screen have no client id to give, and
    // must keep asking the question they ask today.
    mocks.validateRedirectUri.mockResolvedValue({ allowed: true });

    const options = authQueries.redirectValidation(URL_UNDER_TEST);
    await (options.queryFn as () => Promise<unknown>)();

    expect(mocks.validateRedirectUri.mock.calls[0]?.[0]).toBe(URL_UNDER_TEST);
    expect(mocks.validateRedirectUri.mock.calls[0]?.[1]).toBeUndefined();
  });
});
