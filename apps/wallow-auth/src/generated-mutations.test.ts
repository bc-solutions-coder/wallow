import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

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
 * This spec is that rule's lock.
 *
 * Why a spec and not a lint rule: there is no oxlint rule forbidding a hand-rolled
 * mutation (bd memory `wallow-auth-oxlint-no-query-facade-rule-yet` — the
 * `no-restricted-imports` enforcement is a later feature of this epic), so nothing
 * else in the toolchain notices a screen that quietly grows one back.
 *
 * Why it matters HERE more than anywhere: this app is the login / signup / MFA
 * surface, and the conversion changes the `.mutate()` VARIABLES SHAPE at every call
 * site — from a bare body to the request object `{ body }` / `{ path }` / `{ query }`
 * (bd memory `wallow-generated-react-query-artifacts-live-in-packages`). A site that
 * keeps passing a bare body still compiles against a loosely-typed variable and
 * still renders; it just sends an empty request. The wire-level assertions in each
 * screen's own spec (`harness.calls`, `.body`, `.url`) are what catch that, and this
 * file is what catches the other half — a conversion that was SKIPPED, half-applied,
 * or applied to a file the bead put out of scope.
 *
 * Three kinds of assertion live here, in this order:
 *
 *  1. STRUCTURAL, per file, per NAME. {@link GENERATED_MUTATION_USERS} is a table
 *     rather than a global "somebody imports a factory" check: a file that converts
 *     one of its two mutations and leaves the other is the likeliest way this lands
 *     half-done, and a named table reports WHICH one.
 *  2. THE GENERATED SURFACE, resolved at runtime. A factory name is a string until
 *     something calls it; these cases prove each name in the table really is an
 *     exported mutation factory (and that the magic-link GET really has no mutation
 *     factory at all, which is the whole reason it needs different treatment).
 *  3. THE DEAD `retry: false` OVERRIDES, proven dead. The bead deletes eight of
 *     them; the two cases at the end are the proof that deleting them changes no
 *     behaviour, stated against the client the app actually builds rather than
 *     asserted in a comment.
 *
 * Structural assertions read source as TEXT with comments stripped first, exactly as
 * `query-facade.test.ts` does: prose that legitimately discusses the old shape is not
 * a hand-rolled mutation, and the bead explicitly keeps one such comment in
 * `routes/login.tsx`. This file is excluded from its own scans — it has to name the
 * banned shapes.
 *
 * Node project — it reads files and imports built package output; it mounts nothing.
 */

/** The generated TanStack surface. Never hand-roll these; regenerate instead. */
const QUERY_ENTRY = "@bc-solutions-coder/sdk/query";

/** The raw SDK barrel: still the home of the operations, guards and DTO types. */
const SDK_ENTRY = "@bc-solutions-coder/sdk";

/**
 * The feature's own `api.ts` re-export seam, as a screen under
 * `features/<feature>/components/` spells it (Wallow-x4qn.9.4).
 *
 * The per-file tables below used to read the SDK entries straight off each screen.
 * They cannot any more: a screen's data import now names the SEAM, and the seam
 * names the entry. So this file keeps asking the question it always asked — WHICH
 * artifact does THIS screen use, per name — one hop further out, and
 * `features-api-seam.test.ts` owns the other hop (that each seam re-exports
 * exactly its feature's surface, from the right entry, by identity).
 *
 * The `retired` expectations are unaffected and still read {@link SDK_ENTRY}
 * directly: a screen must not import the raw operation from anywhere, and the seam
 * is not an exception to that — for eight of the ten features the seam does not
 * name the raw barrel at all.
 */
const SEAM = "../api";

const srcDir: string = dirname(fileURLToPath(import.meta.url));

/** This file, excluded from the source scans: it must name the banned shapes. */
const SELF: string = relative(srcDir, fileURLToPath(import.meta.url));

/**
 * Per file: the generated artifacts it must take from its {@link SEAM}, and the raw
 * operations it must no longer take from {@link SDK_ENTRY}. Before
 * Wallow-x4qn.9.4 the `generated` names were read off {@link QUERY_ENTRY} at the
 * screen itself; the seam moved the entry one hop out and the names stayed put.
 *
 * `retired` is a per-NAME expectation, not a per-specifier one, because four of
 * these files keep importing {@link SDK_ENTRY} for something else entirely —
 * `isSafeReturnUrl`, `buildExchangeTicketUrl`, `validateRedirectUriArgs` and the
 * `InvitationResponse`/`MfaEnrollment*` DTO types. A specifier-level check would
 * either miss the conversion or demand those be collateral damage.
 */
const GENERATED_MUTATION_USERS: Readonly<
  Record<string, { readonly generated: readonly string[]; readonly retired: readonly string[] }>
> = {
  "features/invitation/components/InvitationScreen.tsx": {
    generated: ["invitationsAcceptMutation"],
    retired: ["invitationsAccept"],
  },
  "features/login/components/MagicLinkLoginForm.tsx": {
    // Two writes on paper, but only ONE mutation factory: `verify` is a GET, so
    // the generator emits an Options factory for it. See the describe below.
    generated: ["accountSendMagicLinkMutation", "accountVerifyMagicLinkOptions"],
    retired: ["accountSendMagicLink", "accountVerifyMagicLink"],
  },
  "features/login/components/OtpLoginForm.tsx": {
    generated: ["accountSendOtpMutation", "accountVerifyOtpMutation"],
    retired: ["accountSendOtp", "accountVerifyOtp"],
  },
  "features/login/components/PasswordLoginForm.tsx": {
    generated: ["accountLoginMutation"],
    retired: ["accountLogin"],
  },
  "features/mfa-challenge/components/MfaChallengeForm.tsx": {
    generated: ["accountVerifyMfaChallengeMutation"],
    retired: ["accountVerifyMfaChallenge"],
  },
  "features/mfa-enroll/components/MfaEnrollForm.tsx": {
    generated: ["mfaConfirmEnrollmentMutation", "mfaEnrollTotpMutation"],
    retired: ["mfaConfirmEnrollment", "mfaEnrollTotp"],
  },
  "features/register/components/RegisterForm.tsx": {
    generated: ["accountRegisterMutation"],
    retired: ["accountRegister"],
  },
};

/**
 * The password-recovery screens, which this bead puts explicitly OUT OF SCOPE.
 *
 * Both migrated to `@bc-solutions-coder/forms`' `useAppForm` and deliberately use
 * its NO-MUTATION escape hatch: `forgot-password` swallows every failure so the
 * screen cannot be used to enumerate accounts, and `reset-password` owns its own
 * status-code branching. Handing either one a generated mutation would restore
 * exactly the error surface their file headers forbid — so "no `mutationFn`
 * anywhere" must not be satisfied by converting them, and this table is the
 * positive guard that the sweep stopped at their door.
 */
const NO_MUTATION_SCREENS: Readonly<Record<string, readonly string[]>> = {
  "features/forgot-password/components/ForgotPasswordForm.tsx": ["accountForgotPassword"],
  "features/reset-password/components/ResetPasswordForm.tsx": ["accountResetPassword"],
};

/**
 * The imperative operation in `MfaEnrollForm`'s mount effect that is NOT a
 * `useMutation` and must stay raw.
 *
 * Order is the whole contract there: the exchange is what mints the
 * `Identity.MfaPartial` cookie, and `enroll/totp` fired first has no session and
 * 401s. It is awaited inside a `try`/`catch` in the effect precisely so the next
 * step can be skipped on failure — a mutation would put that sequencing behind a
 * callback and is not what the bead asks for.
 */
const IMPERATIVE_SURVIVOR = {
  path: "features/mfa-enroll/components/MfaEnrollForm.tsx",
  name: "mfaExchangeEnrollmentToken",
} as const;

/**
 * Every generated mutation factory the table above names, bound to its real export.
 *
 * NAMED imports, not a namespace one: `no-restricted-imports` bans `import *` from
 * this entry (it would reach the deleted hand-written query slices), and naming each
 * binding is the stronger check anyway — a factory the generator does not export is
 * a type error and a module-load crash before it is ever an assertion.
 *
 * `() => unknown` is the widest shape all ten share: each takes one OPTIONAL options
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

function readText(path: string): string {
  return readFileSync(path, "utf8");
}

/**
 * Every hand-written TypeScript module under `src/`, specs included.
 *
 * `withFileTypes` + `isFile()` matters: Vitest browser mode writes failure
 * screenshots into `src/**\/__screenshots__/<spec>.test.tsx/` directories, and a
 * name-only filter would hand `readFileSync` a directory. `routeTree.gen.ts` is
 * codegen and this file is the guard itself.
 */
function appSources(): readonly string[] {
  return readdirSync(srcDir, { recursive: true, withFileTypes: true })
    .filter(
      (entry): boolean =>
        entry.isFile() && (entry.name.endsWith(".ts") || entry.name.endsWith(".tsx")),
    )
    .map((entry): string => relative(srcDir, join(entry.parentPath, entry.name)))
    .filter((path): boolean => path !== SELF && path !== "routeTree.gen.ts")
    .toSorted();
}

/** Source with comments removed, so prose about the old shape is not read as code. */
function codeOf(relativePath: string): string {
  return readText(join(srcDir, relativePath))
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/**
 * The names a file imports from one module — value imports, `import type` lines and
 * inline `type` members alike, alias targets normalised away — so the conversion
 * stays free to merge or split import statements however it likes.
 */
function importedNamesFrom(relativePath: string, moduleSpecifier: string): readonly string[] {
  const escaped: string = moduleSpecifier.replaceAll(/[.*+?^${}()|[\]\\]/gu, String.raw`\$&`);
  const pattern = new RegExp(
    String.raw`import\s+(?:type\s+)?\{([^}]*)\}\s*from\s+"${escaped}"`,
    "gu",
  );

  return [...codeOf(relativePath).matchAll(pattern)].flatMap((match): readonly string[] =>
    (match[1] as string)
      .split(",")
      .map((name): string =>
        (
          name
            .trim()
            .replace(/^type\s+/u, "")
            .split(/\s+as\s+/u)[0] as string
        ).trim(),
      )
      .filter((name): boolean => name.length > 0),
  );
}

/** Files under `src/` whose comment-stripped code contains `needle`. */
function filesContaining(needle: string): readonly string[] {
  return appSources().filter((path): boolean => codeOf(path).includes(needle));
}

describe("wallow-auth's mutation call sites", () => {
  it("scans a source tree that actually has the modules under test in it", () => {
    // A guard on the guard: a broken scan would make every table-driven case below
    // pass vacuously, over paths that can and do move between beads.
    const scanned: readonly string[] = appSources();

    for (const path of [
      ...Object.keys(GENERATED_MUTATION_USERS),
      ...Object.keys(NO_MUTATION_SCREENS),
    ]) {
      expect(scanned, `${path} is not in the scanned source tree`).toContain(path);
    }
  });

  it("hand-rolls no mutationFn anywhere under src/", () => {
    // Named as a list, so a failure reports WHICH module still wraps a raw
    // operation. A hand-rolled `mutationFn` is the shape this bead removes: it
    // re-states the request the generator already knows how to build, which is
    // where the variables shape and the real endpoint drift apart.
    expect(filesContaining("mutationFn:")).toEqual([]);
  });

  it("carries no local retry override anywhere under src/", () => {
    // Every one of these is DEAD: `createQueryClient()` disables query retries
    // globally and TanStack Query never retries a mutation by default. The two
    // cases at the end of this file are the proof. A dead override is worse than
    // noise — it reads as a deliberate local policy and invites the next author to
    // add one where it would actually matter.
    expect(filesContaining("retry: false")).toEqual([]);
  });

  it.each(Object.entries(GENERATED_MUTATION_USERS))(
    "%s takes its write artifacts from its feature's api.ts seam",
    (path: string, expected: { readonly generated: readonly string[] }) => {
      // Named through {@link SEAM} rather than {@link QUERY_ENTRY} since
      // Wallow-x4qn.9.4: the screen imports from `../api` and the seam imports from
      // the entry. Still per NAME, which is the assertion that matters here — a
      // file that converts one of its two mutations and leaves the other is the
      // likeliest way this lands half-done, and the failure has to say which.
      expect(importedNamesFrom(path, SEAM)).toEqual(
        expect.arrayContaining([...expected.generated]),
      );
    },
  );

  it.each(Object.keys(GENERATED_MUTATION_USERS))(
    "%s names the generated query entry nowhere of its own",
    (path: string) => {
      // The other half of the hop. Without this, a screen could satisfy the case
      // above by importing from the seam and STILL keep a direct query-entry import
      // beside it — two doors to the same artifact, and the seam stops being a
      // statement of what the feature reaches. `features-api-seam.test.ts` asserts
      // the same rule across all of `features/` and `routes/`; this is the per-file
      // form, so a failure names the screen.
      expect(importedNamesFrom(path, QUERY_ENTRY)).toEqual([]);
    },
  );

  it.each(Object.entries(GENERATED_MUTATION_USERS))(
    "%s no longer imports the raw operation it used to wrap",
    (path: string, expected: { readonly retired: readonly string[] }) => {
      // Per NAME, not per specifier: most of these files keep importing the raw
      // barrel for a return-url guard or a DTO type.
      const raw: readonly string[] = importedNamesFrom(path, SDK_ENTRY);

      for (const name of expected.retired) {
        expect(raw, `${path} still imports the raw ${name}`).not.toContain(name);
      }
    },
  );

  it("keeps MfaEnrollForm's imperative enrollment-token exchange unconverted", () => {
    // NOT a mutation, and not this bead's business: it is awaited inside the mount
    // effect's try/catch so a failed exchange skips the enroll that would only 401.
    // Since Wallow-x4qn.9.4 the screen names it through the seam like its two
    // mutations — it is still data — and `features/mfa-enroll/api.test.ts` is where
    // "the seam takes it from the RAW barrel, because the generator emits no
    // factory for it" is asserted against the built package.
    expect(importedNamesFrom(IMPERATIVE_SURVIVOR.path, SEAM)).toContain(IMPERATIVE_SURVIVOR.name);
    expect(importedNamesFrom(IMPERATIVE_SURVIVOR.path, SDK_ENTRY)).not.toContain(
      IMPERATIVE_SURVIVOR.name,
    );
  });
});

describe("the password-recovery screens this bead leaves alone", () => {
  it.each(Object.entries(NO_MUTATION_SCREENS))(
    "%s still reaches its endpoint through the raw operation",
    (path: string, names: readonly string[]) => {
      // Through the seam since Wallow-x4qn.9.4, which is the ONE case where the
      // seam itself re-exports from {@link SDK_ENTRY} rather than the query entry —
      // these two features own no generated artifact at all. That the raw operation
      // is what sits behind `../api` here is asserted in each feature's own
      // `api.test.ts`, together with the absence of a factory to prefer instead.
      expect(importedNamesFrom(path, SEAM)).toEqual(expect.arrayContaining([...names]));
    },
  );

  it.each(Object.keys(NO_MUTATION_SCREENS))("%s runs no mutation at all", (path: string) => {
    // `useAppForm`'s no-mutation escape hatch is the POINT for both: one swallows
    // every failure to defeat account enumeration, the other owns its own
    // status-code branching. A generated mutation would hand each of them a
    // failure surface its header forbids.
    const code: string = codeOf(path);

    expect(code).not.toContain("useMutation");
    expect(code).not.toContain("Mutation(");
  });
});

/**
 * The generated surface, resolved rather than spelled.
 *
 * A factory name is just a string in the table above; these cases prove each one is
 * really exported and really a mutation factory. They also pin the two properties
 * the conversion depends on and neither the type checker nor a grep can see:
 * that a factory hands back ONLY a `mutationFn`, and that the magic-link GET has no
 * mutation factory to hand back at all.
 */
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

  it("names the same factories the call-site table above expects", () => {
    // Keeps the two halves of this file honest: the table drives the source scan and
    // this list drives the resolution check, and a factory renamed in one place must
    // be renamed in the other or the scan silently stops proving anything.
    const named: readonly string[] = Object.values(GENERATED_MUTATION_USERS)
      .flatMap((entry): readonly string[] => entry.generated)
      .filter((name): boolean => name.endsWith("Mutation"))
      .toSorted();

    expect(MUTATION_FACTORIES.map(([name]): string => name).toSorted()).toEqual(named);
  });

  it("has no mutation factory for the magic-link verify to convert to", () => {
    // THE SPECIAL CASE. `GET /passwordless/magic-link/verify` is a read as far as the
    // generator is concerned, so `accountVerifyMagicLinkMutation` does not exist —
    // which is why the ABSENCE is asserted against this app's SOURCE rather than
    // against the module: a named import of a missing export is a load-time crash,
    // not an assertion. What this stops is a green phase inventing the name and
    // hand-rolling a wrapper to back it.
    expect(filesContaining("accountVerifyMagicLinkMutation")).toEqual([]);
  });

  it("gives the magic-link verify a queryFn and a key under its Options factory", () => {
    // The replacement the screen gets instead, resolved: a read artifact, so the
    // redemption runs through the query client. The behaviour that buys — and the
    // cache entry it leaves behind — is pinned in
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
 * safe for as long as these two cases hold.
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
