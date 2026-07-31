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
import { MutationObserver, QueryClient } from "@bc-solutions-coder/query";
import { describe, expect, it } from "vitest";

/**
 * Every write wallow-auth issues goes through a GENERATED `{operation}Mutation()`
 * factory, and no screen hand-rolls a `mutationFn` of its own (Wallow-x4qn.9.3).
 *
 * WHO ENFORCES WHAT, since Wallow-l5x2 split this file in two. The rule itself is
 * lint's: `wallow/no-hand-rolled-mutation` in `apps/wallow-auth/.oxlintrc.json`
 * reports a `mutationFn` property at the offending line, and the same config's
 * `no-restricted-imports` keeps a screen from reaching the generated entry around
 * its feature's `api.ts` seam. What used to sit here instead was a hand-kept table
 * of seven screen paths × the artifact names each one imports, re-read off disk with
 * a regex comment-stripper on every run: it had to be edited whenever a component
 * moved, it could only ever describe the screens someone remembered to list, and it
 * reported the offence in this file rather than in the offending one.
 *
 * WHAT SURVIVES IS RUNTIME FACT ABOUT THE GENERATED SURFACE — things no rule and no
 * grep can see, because they are properties of the built package rather than of the
 * text that names it:
 *
 *  1. Each factory hands back a `mutationFn` and NOTHING else — which is why a screen
 *     with its own `onSuccess` must SPREAD the factory rather than pass it through.
 *  2. The magic-link verify is a GET, so what the generator emits for it is an
 *     Options factory with a `queryFn` and a key, not a mutation at all.
 *  3. The eight deleted `retry: false` overrides were dead, stated as behaviour
 *     against the client this app really builds rather than claimed in a comment.
 *
 * Node project — it imports built package output; it mounts nothing.
 */

/**
 * Every generated mutation factory wallow-auth's screens reach, bound to its real
 * export.
 *
 * NAMED imports, not a namespace one: `no-restricted-imports` bans `import *` from
 * this entry (it would reach the deleted hand-written query slices), and naming each
 * binding is the stronger check anyway — a factory the generator does not export is
 * a type error and a module-load crash before it is ever an assertion. That is also
 * what makes this list self-checking in a way the deleted path table was not.
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
    // through the query client. The behaviour that buys — and the cache entry it
    // leaves behind — is pinned in
    // `features/login/components/MagicLinkLoginForm.verify.test.tsx`.
    const options = accountVerifyMagicLinkOptions({ query: { token: "t" } });

    expect(typeof options.queryFn).toBe("function");
    expect(Array.isArray(options.queryKey)).toBe(true);
  });
});

/**
 * Why the eight deleted `retry: false` overrides were dead.
 *
 * Stated as behaviour against the client this app really builds, not as a claim in
 * a comment: a retry policy that silently changed would turn a one-shot magic-link
 * token or a lockout-counted MFA code into two attempts, and the deletion is only
 * safe for as long as these two cases hold. They are also why no lint rule bans a
 * resurrected `retry: false` — a re-added override would be inert, which is exactly
 * what these two cases prove.
 */
describe("retry policy without a local override", () => {
  it("attempts a failing query exactly once on a facade client", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    let attempts = 0;

    await expect(
      client.fetchQuery({
        queryKey: ["retry-probe"],
        queryFn: () => {
          attempts += 1;
          throw new Error("nope");
        },
      }),
    ).rejects.toThrow("nope");

    expect(attempts).toBe(1);
  });

  it("attempts a failing mutation exactly once with no retry option at all", async () => {
    // The mutation half, which `createQueryClient()` does NOT configure — and does
    // not have to: TanStack Query's default mutation retry is none, so
    // `InvitationScreen`'s `retry: false` on its accept mutation was always a no-op.
    const client = new QueryClient();
    let attempts = 0;

    const observer = new MutationObserver(client, {
      mutationFn: async (): Promise<never> => {
        attempts += 1;
        throw new Error("nope");
      },
    });

    await expect(observer.mutate()).rejects.toThrow("nope");

    expect(attempts).toBe(1);
  });
});
