import {
  accountLoginMutation,
  accountRegisterMutation,
  accountSendMagicLinkMutation,
  accountSendOtpMutation,
  accountVerifyMagicLinkOptions,
  accountVerifyMfaChallengeMutation,
  accountVerifyOtpMutation,
  invitationsAcceptMutation,
  mfaConfirmEnrollmentMutation,
  mfaEnrollTotpMutation,
} from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

/**
 * Every write wallow-auth issues goes through a GENERATED `{operation}Mutation()`
 * factory. `wallow/no-hand-rolled-mutation` enforces the rule at the offending line,
 * and `packages/sdk` proves an artifact EXISTS for every operation — neither says
 * what a factory hands back, which is what this file covers.
 *
 * Node project — it imports built package output; it mounts nothing.
 */

/**
 * Every generated mutation factory wallow-auth's screens reach, bound to its real
 * export.
 *
 * NAMED imports, not a namespace one: `no-restricted-imports` bans `import *` from
 * this entry, and naming each binding is the stronger check anyway — a factory the
 * generator does not export is a module-load crash before it is ever an assertion.
 *
 * `() => unknown` is the widest shape all nine share: each takes one OPTIONAL options
 * argument, so calling with none is valid for every one of them.
 */
const MUTATION_FACTORIES: readonly (readonly [string, () => unknown])[] = [
  ["accountLoginMutation", accountLoginMutation],
  ["accountRegisterMutation", accountRegisterMutation],
  ["accountSendMagicLinkMutation", accountSendMagicLinkMutation],
  ["accountSendOtpMutation", accountSendOtpMutation],
  ["accountVerifyMfaChallengeMutation", accountVerifyMfaChallengeMutation],
  ["accountVerifyOtpMutation", accountVerifyOtpMutation],
  ["invitationsAcceptMutation", invitationsAcceptMutation],
  ["mfaConfirmEnrollmentMutation", mfaConfirmEnrollmentMutation],
  ["mfaEnrollTotpMutation", mfaEnrollTotpMutation],
];

describe("the generated artifacts wallow-auth's screens name", () => {
  it.each(MUTATION_FACTORIES)(
    "%s hands back a mutationFn and nothing else",
    (name: string, factory: () => unknown) => {
      // bd memory `when-migrating-a-form-to-bc-solutions-coder`: the factories bake
      // in NO `onSuccess`. That is why `InvitationScreen`, whose success arm is a
      // full-page navigation declared on the hook rather than at the call site, has
      // to SPREAD the factory into its `useMutation` options instead of passing it
      // straight through — a replacement would silently drop the navigation.
      expect(Object.keys(factory() as object).toSorted(), name).toEqual(["mutationFn"]);
    },
  );

  it("gives the magic-link verify a queryFn and a key under its Options factory", () => {
    // THE SPECIAL CASE. `GET /passwordless/magic-link/verify` is a read as far as the
    // generator is concerned, so there is no `accountVerifyMagicLinkMutation` to
    // convert to and the screen gets a read artifact instead: the redemption runs
    // through the query client.
    const options = accountVerifyMagicLinkOptions({ query: { token: "t" } });

    expect(typeof options.queryFn).toBe("function");
    expect(Array.isArray(options.queryKey)).toBe(true);
  });
});
