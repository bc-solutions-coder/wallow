import { describe, expect, it } from "vitest";

/**
 * Login feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4 to bring wallow-auth
 * in line with wallow-web. The four login panels and the route that hosts them
 * import from `../api`; every artifact behind it is GENERATED from
 * `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence. A re-export is the one construct whose
 * correctness no call site can observe: a hand-written `accountLoginMutation` that
 * closes over its own `fetch` has the same name, the same call shape and the same
 * type, and a screen driving it passes every behavioural spec while sending a
 * request the OpenAPI document does not describe. `toBe` is what rules that out.
 *
 * This seam is the app's widest because `login` is four panels and a route:
 * password, OTP, magic-link and external providers, plus the fork branding the
 * login route paints the screen with. Two of the nine are `{op}QueryKey`
 * factories that only the specs need — `ExternalProviders.test.tsx` and
 * `MagicLinkLoginForm.verify.test.tsx` read the cache the screens write, and they
 * resolve their keys through the seam for the same reason the screens do: a key
 * built anywhere else is a key the screen never wrote.
 *
 * The magic-link asymmetry is worth reading twice. `send` is a POST and gets a
 * `{op}Mutation()`; `verify` is a GET, so the generator emits
 * `accountVerifyMagicLinkOptions` and NO mutation factory — the redemption runs
 * through `queryClient.fetchQuery`. Both halves are named below. The ABSENCE of a
 * third needs no assertion anywhere: unlike the three raw operations, whose
 * generated factories DO exist and are deliberately not adopted, this one was never
 * emitted — so a screen reaching for it fails to compile at the import.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = [
  "accountGetExternalProvidersOptions",
  "accountGetExternalProvidersQueryKey",
  "accountLoginMutation",
  "accountSendMagicLinkMutation",
  "accountSendOtpMutation",
  "accountVerifyMagicLinkOptions",
  "accountVerifyMagicLinkQueryKey",
  "accountVerifyOtpMutation",
  "clientBrandingGetBrandingOptions",
];

describe("api.ts re-exports the SDK login query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountLoginMutation).toBe(query.accountLoginMutation);
    expect(api.accountSendOtpMutation).toBe(query.accountSendOtpMutation);
    expect(api.accountVerifyOtpMutation).toBe(query.accountVerifyOtpMutation);
    expect(api.accountSendMagicLinkMutation).toBe(query.accountSendMagicLinkMutation);
    expect(api.accountVerifyMagicLinkOptions).toBe(query.accountVerifyMagicLinkOptions);
    expect(api.accountVerifyMagicLinkQueryKey).toBe(query.accountVerifyMagicLinkQueryKey);
    expect(api.accountGetExternalProvidersOptions).toBe(query.accountGetExternalProvidersOptions);
    expect(api.accountGetExternalProvidersQueryKey).toBe(query.accountGetExternalProvidersQueryKey);
    expect(api.clientBrandingGetBrandingOptions).toBe(query.clientBrandingGetBrandingOptions);
  });

  it("exposes nothing beyond the artifacts the login feature uses", () => {
    // The seam is a statement of what this feature reaches. An extra re-export is
    // either a screen's data import that nobody wrote down, or a leftover from a
    // panel that was deleted.
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("the magic-link pair the seam re-exports", () => {
  it("gives the POST a mutation factory and the GET an options factory", () => {
    // `GET /passwordless/magic-link/verify` is a read to the generator, so the
    // redemption runs through the query client rather than a mutation. The two
    // shapes are asserted rather than described, because the asymmetry is what a
    // future reader will try to tidy away.
    expect(Object.keys(query.accountSendMagicLinkMutation()).toSorted()).toEqual(["mutationFn"]);
    expect(typeof query.accountVerifyMagicLinkOptions({ query: { token: "t" } }).queryFn).toBe(
      "function",
    );
  });
});
