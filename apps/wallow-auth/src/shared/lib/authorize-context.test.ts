import { QueryClient } from "@bc-solutions-coder/query";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { beforeEach, describe, expect, it } from "vitest";

import { forkResolvedBranding } from "./branding";

import {
  fetchAuthorizeContext,
  resolveAuthorizeTransaction,
  resolveTransactionBranding,
} from "./authorize-context";

/**
 * The layout-level client context: which locations count as an authorize
 * transaction, how the context is fetched through the query cache, and how a
 * resolved context maps onto screen branding. Every arm that is not a resolved
 * third-party client — wrong path, unsafe returnUrl, failed lookup, first-party
 * client — collapses to `null`/fork chrome; branding is chrome and must never
 * block a screen.
 */

const RETURN_URL = "/connect/authorize?client_id=acme-web&redirect_uri=https%3A%2F%2Fapp.acme.test";
const CONTEXT_PATH = "/v1/identity/auth/authorize-context";

/** An `AuthorizeContextResponse` for a third-party client, overridable. */
function context(overrides: Record<string, unknown> = {}) {
  return {
    clientId: "acme-web",
    displayName: "Acme",
    tagline: "Acme things",
    logoUrl: "https://cdn.test/acme.svg",
    themeJson: null,
    organizationName: "Acme Corp",
    firstParty: false,
    scopes: [{ name: "openid", description: null }],
    ...overrides,
  };
}

describe("resolveAuthorizeTransaction", () => {
  it("identifies a transaction screen carrying a safe authorize returnUrl", () => {
    expect(resolveAuthorizeTransaction("/login", { returnUrl: RETURN_URL })).toEqual({
      returnUrl: RETURN_URL,
    });
  });

  it("covers every in-transaction screen", () => {
    const screens: readonly string[] = [
      "/login",
      "/register",
      "/consent",
      "/accept-terms",
      "/mfa/challenge",
      "/mfa/enroll",
      "/verify-email",
      "/forgot-password",
    ];

    for (const pathname of screens) {
      expect(resolveAuthorizeTransaction(pathname, { returnUrl: RETURN_URL })).not.toBeNull();
    }
  });

  it("carries the scope through when the link has one", () => {
    expect(
      resolveAuthorizeTransaction("/consent", { returnUrl: RETURN_URL, scope: "openid profile" }),
    ).toEqual({ returnUrl: RETURN_URL, scope: "openid profile" });
  });

  it("treats a trailing slash as the same screen", () => {
    // `/verify-email/` is the index route's canonical spelling.
    expect(resolveAuthorizeTransaction("/verify-email/", { returnUrl: RETURN_URL })).not.toBeNull();
  });

  it("answers null for a screen outside the transaction set", () => {
    // The email-link screens render fork chrome even when a crafted link hands
    // them a plausible returnUrl — nothing may fetch client branding for them.
    for (const pathname of [
      "/error",
      "/reset-password",
      "/verify-email/confirm",
      "/setup",
      "/privacy",
    ]) {
      expect(resolveAuthorizeTransaction(pathname, { returnUrl: RETURN_URL })).toBeNull();
    }
  });

  it("answers null when the link carries no returnUrl", () => {
    expect(resolveAuthorizeTransaction("/login", {})).toBeNull();
  });

  it("refuses an unsafe returnUrl rather than forwarding it", () => {
    for (const returnUrl of ["", "//evil.example/steal", "https://evil.example/steal"]) {
      expect(resolveAuthorizeTransaction("/login", { returnUrl })).toBeNull();
    }
  });

  it("refuses a safe returnUrl that is not the authorize endpoint", () => {
    // Locally safe, but no pending transaction can live there — the endpoint
    // would refuse it, so the loader does not ask.
    expect(resolveAuthorizeTransaction("/login", { returnUrl: "/dashboard" })).toBeNull();
    expect(resolveAuthorizeTransaction("/login", { returnUrl: "/connect/authorized" })).toBeNull();
  });

  it("accepts the bare authorize path with no query of its own", () => {
    expect(resolveAuthorizeTransaction("/login", { returnUrl: "/connect/authorize" })).toEqual({
      returnUrl: "/connect/authorize",
    });
  });
});

let harness: SdkHarness;
let queryClient: QueryClient;

/** Every recorded request to the context endpoint. */
function contextCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === CONTEXT_PATH);
}

beforeEach(() => {
  harness = createPassthroughHarness();
  harness.resolveJson(context());
  // No retries: a failure must collapse to `null` on the first attempt, the
  // same way the apps' own query clients are tuned in specs.
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
});

describe("fetchAuthorizeContext", () => {
  it("resolves the transaction's context keyed by its returnUrl and scope", async () => {
    const result = await fetchAuthorizeContext({
      queryClient,
      client: harness.sdk.client,
      pathname: "/login",
      search: { returnUrl: RETURN_URL, scope: "openid" },
    });

    expect(result).toEqual(context());
    expect(contextCalls()).toHaveLength(1);

    const url = new URL(contextCalls()[0]?.url ?? "");
    expect(url.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(url.searchParams.get("scope")).toBe("openid");
  });

  it("answers from the cache on a repeat lookup for the same transaction", async () => {
    // The whole transaction — login through consent, same returnUrl — resolves
    // the context ONCE; later screens are cache hits, not new requests.
    const search = { returnUrl: RETURN_URL };

    await fetchAuthorizeContext({
      queryClient,
      client: harness.sdk.client,
      pathname: "/login",
      search,
    });
    await fetchAuthorizeContext({
      queryClient,
      client: harness.sdk.client,
      pathname: "/consent",
      search,
    });

    expect(contextCalls()).toHaveLength(1);
  });

  it("asks nothing for a location outside a transaction", async () => {
    const result = await fetchAuthorizeContext({
      queryClient,
      client: harness.sdk.client,
      pathname: "/error",
      search: { returnUrl: RETURN_URL },
    });

    expect(result).toBeNull();
    expect(harness.calls).toEqual([]);
  });

  it("collapses a refused lookup to null rather than throwing", async () => {
    // The endpoint 404s an expired transaction, an unknown client and a
    // malformed returnUrl alike; all of them mean fork chrome, never a crash.
    harness.rejectJson({}, 404);

    const result = await fetchAuthorizeContext({
      queryClient,
      client: harness.sdk.client,
      pathname: "/login",
      search: { returnUrl: RETURN_URL },
    });

    expect(result).toBeNull();
  });
});

describe("resolveTransactionBranding", () => {
  it("maps a third-party context onto the client's chrome and attribution", () => {
    const branded = resolveTransactionBranding(context());

    expect(branded?.branding.name).toBe("Acme");
    expect(branded?.branding.tagline).toBe("Acme things");
    expect(branded?.branding.logoUrl).toBe("https://cdn.test/acme.svg");
    expect(branded?.organizationName).toBe("Acme Corp");
  });

  it("keeps the fork's theme mode when the client curates no theme", () => {
    expect(resolveTransactionBranding(context())?.branding.defaultMode).toBe(
      forkResolvedBranding.defaultMode,
    );
  });

  it("collapses a first-party client to fork chrome", () => {
    // The fork's own apps ARE the fork: no client overlay, and no
    // "by <organization>" line — attribution exists to flag a third party.
    expect(resolveTransactionBranding(context({ firstParty: true }))).toBeNull();
  });

  it("answers null for no context at all", () => {
    expect(resolveTransactionBranding(null)).toBeNull();
    expect(resolveTransactionBranding(undefined)).toBeNull();
  });
});
