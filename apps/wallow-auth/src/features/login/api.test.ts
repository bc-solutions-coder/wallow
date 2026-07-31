import { describe, expect, it } from "vitest";

/**
 * The login feature's `api.ts`: a re-export seam over the SDK query entry.
 *
 * Asserted by identity (`toBe`), not presence: a hand-written
 * `accountLoginMutation` closing over its own `fetch` has the same name and
 * type, and every behavioural spec would still pass.
 *
 * Magic-link `verify` is a GET, so the generator emits an Options factory and
 * no mutation.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

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
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("the magic-link pair the seam re-exports", () => {
  it("gives the POST a mutation factory and the GET an options factory", () => {
    expect(Object.keys(query.accountSendMagicLinkMutation()).toSorted()).toEqual(["mutationFn"]);
    expect(typeof query.accountVerifyMagicLinkOptions({ query: { token: "t" } }).queryFn).toBe(
      "function",
    );
  });
});
