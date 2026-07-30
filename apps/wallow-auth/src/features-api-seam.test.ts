import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Every wallow-auth feature that talks to the API talks to it through ONE module:
 * its own `features/<feature>/api.ts` (Wallow-x4qn.9.4). This spec is that
 * boundary's lock, and it is a lock on the BOUNDARY rather than on the files —
 * asserting that ten `api.ts` files exist would pass just as happily with every
 * screen still importing the SDK behind their backs, which is the exact state
 * this bead ends.
 *
 * Why the boundary is worth a spec at all. wallow-web has had the seam since
 * Wallow-pu6a; wallow-auth grew without it, so its screens name generated
 * artifacts directly and there is nowhere to write down what a feature's data
 * surface IS. The seam buys three things a direct import cannot:
 *
 *  1. A feature's data surface is enumerable. `api.ts` is the list of operations
 *     the feature is allowed to reach, reviewable in one file, and a screen that
 *     starts calling a fourteenth endpoint has to say so there first.
 *  2. Regeneration lands in one place per feature. The artifacts behind the seam
 *     are GENERATED from `packages/sdk/openapi/v1.json`; a renamed operation
 *     breaks one re-export instead of five screens.
 *  3. The `api.test.ts` beside each seam can assert things a call site cannot —
 *     that a re-export really is the generated artifact and not a look-alike
 *     wrapper, and (for the three raw operations) that the generated factory the
 *     SDK does emit for them is deliberately left unadopted.
 *
 * Why a spec and NOT a lint rule: `.oxlintrc.json` does not yet forbid importing
 * `@bc-solutions-coder/sdk/query` from a screen — that enforcement is a later
 * feature of this epic (bd memory `wallow-auth-oxlint-no-query-facade-rule-yet`),
 * and its one existing override merely WHITELISTS `apps/**\/src/features/*\/api.test.ts`
 * so an identity spec may reach past the seam it tests. Until the rule lands,
 * nothing in the toolchain notices a screen that reaches around the seam.
 *
 * The scope is `src/features/**` and `src/app/routes/**`. It is deliberately not the
 * whole app: `router.tsx`, `start.ts` and `routes/__root.tsx` build the
 * request-scoped SDK itself and must name the package, and the app-level guard
 * specs beside this one (`query-facade.test.ts`, `generated-mutations.test.ts`)
 * have to name the entries they police.
 *
 * What the seam does NOT own, and why the distinction is load-bearing: the raw
 * barrel is also the home of PURE helpers and DTO types — `isSafeReturnUrl`,
 * `buildExchangeTicketUrl`, `consentInfoArgs`, `validateRedirectUriArgs`,
 * `InvitationResponse`. None of them issues a request, so routing them through a
 * seam would inflate every `api.ts` into a re-export of the barrel and lose the
 * one property that makes the seam readable: that it lists the feature's
 * ENDPOINTS. {@link RAW_DATA_OPERATIONS} is the closed set of raw exports that
 * are requests, and the last describe proves the split rather than asserting it.
 *
 * Structural assertions read source as TEXT with comments stripped first, exactly
 * as `query-facade.test.ts` and `generated-mutations.test.ts` do: prose that
 * legitimately names an SDK entry (this file, and several seam doc comments) is
 * not an import. This file is excluded from its own scans.
 *
 * Node project — it reads files and dynamically imports built package output; it
 * mounts nothing.
 */

/** The generated TanStack surface: `{op}Options()`, `{op}Mutation()`, `{op}QueryKey()`. */
const QUERY_ENTRY = "@bc-solutions-coder/sdk/query";

/** The raw barrel: the operations, the guards, the url builders and the DTO types. */
const SDK_ENTRY = "@bc-solutions-coder/sdk";

/**
 * The raw barrel exports that ARE data, and whose seam takes them from
 * {@link SDK_ENTRY} rather than {@link QUERY_ENTRY} because the screen behind it
 * deliberately does not use the generated `{op}Mutation()` factory.
 *
 * The factory is not missing. The generator emits one for EVERY non-GET operation
 * regardless of who calls it, so all three exist on the query entry; each feature's
 * own `api.test.ts` states that and pins the non-adoption instead.
 *
 *  - `accountForgotPassword` / `accountResetPassword` back the password-recovery
 *    screens, which use `@bc-solutions-coder/forms`' NO-MUTATION escape hatch
 *    (one swallows every failure to defeat account enumeration, the other owns
 *    its own status-code branching) and are explicitly excluded from the
 *    generated-mutation conversion by Wallow-x4qn.9.3. They call the operation
 *    inside `useAppForm`'s `onSubmit`, which is a shape no `{op}Mutation()` factory
 *    fits.
 *  - `mfaExchangeEnrollmentToken` is awaited imperatively inside `MfaEnrollForm`'s
 *    mount effect, before `enroll/totp`, because it is what mints the
 *    `Identity.MfaPartial` cookie the enroll call needs — sequencing a mutation
 *    could not express.
 *
 * All three are still DATA. The seam exists so `api.ts` is the feature's only
 * data import, not so it is the feature's only *generated* import, and the last
 * describe pins that none of the three is itself on the query entry — so a seam
 * reaching the barrel for them is not someone missing a same-named export.
 */
const RAW_DATA_OPERATIONS: ReadonlySet<string> = new Set([
  "accountForgotPassword",
  "accountResetPassword",
  "mfaExchangeEnrollmentToken",
]);

/**
 * Every module under `src/features/**` and `src/app/routes/**` that reaches the API,
 * the seam it must reach it through, and the exact artifacts it takes from there.
 *
 * The KEYS are the migration's checklist: each one imported an SDK entry directly
 * before this bead. The `names` are the anti-drop guard — a rewrite that collapses
 * `{ accountSendOtpMutation, accountVerifyOtpMutation }` down to one still
 * compiles and still renders, and leaves a screen that can request a code but
 * never redeem it.
 *
 * Specs are in the table, not exempt from it. `ExternalProviders.test.tsx` and
 * `MagicLinkLoginForm.verify.test.tsx` each name a generated `{op}QueryKey` to
 * seed or read the cache, which is the same data import a screen makes; wallow-web
 * has no spec under `features/` that reaches past a seam, and the one lint override
 * that permits it covers `api.test.ts` alone. A spec that resolves keys from
 * somewhere other than its feature's surface is also how a spec drifts into
 * asserting against a cache entry the screen never writes.
 *
 * `owner` is stated rather than derived from the path because `routes/login.tsx`
 * is the case that matters: the login route reads fork branding for the screen it
 * hosts, so its data import belongs to the LOGIN feature's seam even though the
 * file lives outside `features/`.
 */
interface DataConsumer {
  /** The feature dir whose `api.ts` owns these artifacts. */
  readonly owner: string;
  /** The specifier the file must import from, as written in its source. */
  readonly seam: string;
  /** The artifacts it takes from the seam. */
  readonly names: readonly string[];
}

const DATA_CONSUMERS: Readonly<Record<string, DataConsumer>> = {
  "features/consent/components/ConsentScreen.tsx": {
    owner: "consent",
    seam: "../api",
    names: ["appsGetConsentInfoOptions"],
  },
  "features/forgot-password/components/ForgotPasswordForm.tsx": {
    owner: "forgot-password",
    seam: "../api",
    names: ["accountForgotPassword"],
  },
  "features/invitation/components/InvitationScreen.tsx": {
    owner: "invitation",
    seam: "../api",
    names: ["invitationsAcceptMutation", "invitationsVerifyOptions"],
  },
  "features/login/components/ExternalProviders.test.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountGetExternalProvidersQueryKey"],
  },
  "features/login/components/ExternalProviders.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountGetExternalProvidersOptions"],
  },
  "features/login/components/MagicLinkLoginForm.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountSendMagicLinkMutation", "accountVerifyMagicLinkOptions"],
  },
  "features/login/components/MagicLinkLoginForm.verify.test.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountVerifyMagicLinkQueryKey"],
  },
  "features/login/components/OtpLoginForm.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountSendOtpMutation", "accountVerifyOtpMutation"],
  },
  "features/login/components/PasswordLoginForm.tsx": {
    owner: "login",
    seam: "../api",
    names: ["accountLoginMutation"],
  },
  "features/logout/components/LogoutScreen.tsx": {
    owner: "logout",
    seam: "../api",
    names: ["accountValidateRedirectUriOptions"],
  },
  "features/mfa-challenge/components/MfaChallengeForm.tsx": {
    owner: "mfa-challenge",
    seam: "../api",
    names: ["accountValidateRedirectUriOptions", "accountVerifyMfaChallengeMutation"],
  },
  "features/mfa-enroll/components/MfaEnrollForm.tsx": {
    owner: "mfa-enroll",
    seam: "../api",
    names: ["mfaConfirmEnrollmentMutation", "mfaEnrollTotpMutation", "mfaExchangeEnrollmentToken"],
  },
  "features/register/components/RegisterForm.tsx": {
    owner: "register",
    seam: "../api",
    names: [
      "accountGetClientTenantOptions",
      "accountGetExternalProvidersOptions",
      "accountRegisterMutation",
    ],
  },
  "features/reset-password/components/ResetPasswordForm.tsx": {
    owner: "reset-password",
    seam: "../api",
    names: ["accountResetPassword"],
  },
  "features/verify-email/components/VerifyEmailConfirm.tsx": {
    owner: "verify-email",
    seam: "../api",
    names: ["accountVerifyEmailOptions"],
  },
  "app/routes/login.tsx": {
    owner: "login",
    // An ALIAS seam, not a relative one: routes reach a feature only through its
    // barrel now, so this names the barrel and the assertion below branches on
    // the leading `@`.
    seam: "@features/login",
    names: ["clientBrandingGetBrandingOptions"],
  },
};

/**
 * The feature dirs that reach no endpoint and therefore get NO seam.
 *
 * Stated positively so the sweep cannot be "finished" by scattering empty
 * `api.ts` files across `src/features`: an `api.ts` in front of a screen that
 * renders static copy is a file to maintain that documents nothing. These five
 * arrived after the plan was written, which is why the bead asks for the greps
 * rather than the plan's list.
 */
const NO_SEAM_FEATURES: readonly string[] = [
  "accept-terms",
  "error",
  "not-found",
  "privacy",
  "terms",
];

/**
 * A pure helper the seams must leave on the raw barrel.
 *
 * The anti-overreach guard for {@link RAW_DATA_OPERATIONS}: `isSafeReturnUrl` is
 * a synchronous predicate over a string used by six modules here, and a green
 * phase that reads "api.ts is the feature's only SDK import" as "re-export
 * everything" would pull it — and the DTO types beside it — behind six seams,
 * turning each one from an endpoint list into a second barrel.
 */
const PURE_HELPER = "isSafeReturnUrl";

const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");

/** This file, excluded from the source scans: it must name the banned entries. */
const SELF: string = relative(srcDir, fileURLToPath(import.meta.url));

/** `features/<feature>/api.ts` and its co-located identity spec. */
const SEAM_FILE = /^features\/[^/]+\/api(?:\.test)?\.ts$/u;

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
    .filter((path): boolean => path !== SELF && !path.endsWith("routeTree.gen.ts"))
    .toSorted();
}

/** Source with comments removed, so prose about an entry is not read as an import. */
function codeOf(relativePath: string): string {
  return readText(join(srcDir, relativePath))
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/** Split an `{ a, type B, c as d }` clause into the source names it binds. */
function clauseNames(clause: string): readonly string[] {
  return clause
    .split(",")
    .map((name): string =>
      (
        name
          .trim()
          .replace(/^type\s+/u, "")
          .split(/\s+as\s+/u)[0] as string
      ).trim(),
    )
    .filter((name): boolean => name.length > 0);
}

function specifierPattern(keyword: string, moduleSpecifier: string): RegExp {
  const escaped: string = moduleSpecifier.replaceAll(/[.*+?^${}()|[\]\\]/gu, String.raw`\$&`);

  return new RegExp(String.raw`${keyword}\s+(?:type\s+)?\{([^}]*)\}\s*from\s+"${escaped}"`, "gu");
}

/**
 * The names a file imports from one module — value imports, `import type` lines
 * and inline `type` members alike, alias targets normalised away — so the
 * migration stays free to merge or split import statements however it likes.
 */
function importedNamesFrom(relativePath: string, moduleSpecifier: string): readonly string[] {
  return [...codeOf(relativePath).matchAll(specifierPattern("import", moduleSpecifier))].flatMap(
    (match): readonly string[] => clauseNames(match[1] as string),
  );
}

/** The same, for a re-export: `export { a, b } from "…"`. */
function exportedNamesFrom(relativePath: string, moduleSpecifier: string): readonly string[] {
  return [...codeOf(relativePath).matchAll(specifierPattern("export", moduleSpecifier))].flatMap(
    (match): readonly string[] => clauseNames(match[1] as string),
  );
}

/**
 * Every module specifier a file pulls from: `import … from`, `export … from`,
 * bare side-effect imports, and DYNAMIC `import("…")`. Read off comment-stripped
 * code.
 *
 * The third pattern is the one that is easy to leave out, and this comment used
 * to read "bare side-effect imports alike" as if it covered the case — it did
 * not. `await import("…")` matches neither of the first two: there is no `from`,
 * and `import(` is neither line-anchored nor followed by whitespace. A dynamic
 * import is exactly how a module reaches something it is not supposed to reach at
 * module scope, so a boundary guard blind to it is a boundary guard with a hole
 * shaped like the violation it exists to catch. `src/zone-dag.test.ts` carries the
 * same three patterns for the same reason.
 *
 * Template-literal and variable specifiers stay out of scope — they cannot be
 * judged statically, and this app has none.
 */
function moduleSpecifiers(relativePath: string): readonly string[] {
  const code: string = codeOf(relativePath);

  return [
    ...[...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/^\s*import\s+"([^"]+)"/gmu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/\bimport\(\s*"([^"]+)"\s*\)/gu)].map(
      (match): string => match[1] as string,
    ),
  ];
}

/** `src/features/**` and `src/app/routes/**` minus the seam files, which may reach past it. */
function boundaryScope(): readonly string[] {
  return appSources().filter(
    (path): boolean =>
      (path.startsWith("features/") || path.startsWith("app/routes/")) && !SEAM_FILE.test(path),
  );
}

function seamPath(owner: string): string {
  return `features/${owner}/api.ts`;
}

/** The feature dirs the table above says own a seam. */
function seamOwners(): readonly string[] {
  return [
    ...new Set(Object.values(DATA_CONSUMERS).map((consumer): string => consumer.owner)),
  ].toSorted();
}

/** Everything the consumers of one feature's seam take from it. */
function seamSurface(owner: string): readonly string[] {
  return [
    ...new Set(
      Object.values(DATA_CONSUMERS)
        .filter((consumer): boolean => consumer.owner === owner)
        .flatMap((consumer): readonly string[] => consumer.names),
    ),
  ].toSorted();
}

function generatedSurface(owner: string): readonly string[] {
  return seamSurface(owner).filter((name): boolean => !RAW_DATA_OPERATIONS.has(name));
}

function rawSurface(owner: string): readonly string[] {
  return seamSurface(owner).filter((name): boolean => RAW_DATA_OPERATIONS.has(name));
}

interface PackageManifest {
  readonly exports?: Readonly<Record<string, Readonly<Record<string, string>>>>;
}

/**
 * Import one of the SDK's published entries THROUGH wallow-auth's own link, via
 * the subpath that copy's exports map names.
 *
 * A computed specifier rather than a literal, for two reasons: it resolves the
 * package the way this app's bundler does, and a namespace import of
 * {@link QUERY_ENTRY} is banned outside `api.test.ts` by the one
 * `no-restricted-imports` override there is (it would reach the deleted
 * hand-written query slices). Same shape as `query-facade.test.ts`'s facade import.
 */
async function importSdkEntry(subpath: string): Promise<Record<string, unknown>> {
  const packageDir: string = join(appDir, "node_modules", SDK_ENTRY);

  expect(existsSync(packageDir), `${SDK_ENTRY} is not linked into wallow-auth`).toBe(true);

  const manifest = JSON.parse(readText(join(packageDir, "package.json"))) as PackageManifest;
  const entry: string | undefined = manifest.exports?.[subpath]?.["import"];

  expect(entry, `${SDK_ENTRY} declares no "${subpath}" import entry`).toBeTruthy();

  const entryPath: string = join(packageDir, entry as string);

  expect(existsSync(entryPath), `${SDK_ENTRY} is not built (${entry} missing)`).toBe(true);

  return (await import(pathToFileURL(entryPath).href)) as Record<string, unknown>;
}

describe("wallow-auth's feature data seams", () => {
  it("scans a source tree that actually has the modules under test in it", () => {
    // A guard on the guard: a broken walk would make every table-driven case
    // below pass vacuously, over paths that do move between beads.
    const scanned: readonly string[] = appSources();

    for (const path of Object.keys(DATA_CONSUMERS)) {
      expect(scanned, `${path} is not in the scanned source tree`).toContain(path);
    }

    expect(boundaryScope().length).toBeGreaterThan(Object.keys(DATA_CONSUMERS).length);
  });

  it("declares a seam specifier that really points at its owning feature's api module", () => {
    // The other half of the guard: `owner` drives every expectation about the
    // seam's contents while `seam` drives the source scan, so a mismatch would
    // have this spec checking one feature's file against another's surface.
    for (const [path, consumer] of Object.entries(DATA_CONSUMERS)) {
      // Two seam shapes, two resolutions. A RELATIVE seam is resolved against the
      // consumer's own directory and must land on the owner's api module. An
      // ALIAS seam does not resolve that way at all — `join(dirname(path), "@features/login")`
      // is a nonsense path — so it is compared to the barrel name directly.
      const resolved: string = consumer.seam.startsWith("@")
        ? consumer.seam
        : join(dirname(path), consumer.seam);
      const expected: string = consumer.seam.startsWith("@")
        ? `@features/${consumer.owner}`
        : `features/${consumer.owner}/api`;

      expect(resolved, `${path} declares the wrong seam`).toBe(expected);
    }
  });

  it.each(seamOwners())("features/%s owns an api.ts seam", (owner: string) => {
    const seam: string = seamPath(owner);

    expect(existsSync(join(srcDir, seam)), `${seam} does not exist`).toBe(true);
  });

  it.each(seamOwners())("features/%s owns a co-located api.test.ts", (owner: string) => {
    // The identity spec is half the seam. A re-export is the one construct whose
    // correctness a call site cannot observe: a hand-written wrapper with the
    // same name type-checks, renders, and sends a subtly different request.
    const spec = `features/${owner}/api.test.ts`;

    expect(existsSync(join(srcDir, spec)), `${spec} does not exist`).toBe(true);
  });
});

describe("each seam re-exports exactly its feature's data surface", () => {
  it.each(seamOwners())(
    "features/%s/api.ts takes exactly the generated artifacts its feature uses",
    (owner: string) => {
      // EXACT, not `arrayContaining`. "Thin" is the property under test: a seam
      // that re-exports the feature's surface plus six artifacts nothing calls is
      // no longer a readable statement of what the feature reaches, and a stale
      // re-export is what survives a screen being deleted.
      expect(exportedNamesFrom(seamPath(owner), QUERY_ENTRY).toSorted()).toEqual(
        generatedSurface(owner),
      );
    },
  );

  it.each(seamOwners())(
    "features/%s/api.ts takes exactly the raw operations its feature uses",
    (owner: string) => {
      // Empty for eight of the ten. The two password-recovery seams and
      // mfa-enroll's imperative token exchange are the only raw data operations
      // in the app; anything else appearing here is a pure helper or a DTO type
      // that has been pulled behind a seam it does not belong to.
      expect(exportedNamesFrom(seamPath(owner), SDK_ENTRY).toSorted()).toEqual(rawSurface(owner));
    },
  );

  it.each(seamOwners())("features/%s/api.ts is a re-export and nothing else", (owner: string) => {
    // No imports, no local declarations, no `export *`. A seam that imports
    // something has begun to be a module with behaviour, and behaviour behind a
    // name that looks like a generated artifact is precisely what the co-located
    // identity spec exists to catch — better to make it structurally impossible.
    // `export *` is banned for the same reason the lists above are exact: it
    // states nothing about what the feature reaches.
    const seam: string = seamPath(owner);
    const code: string = codeOf(seam);
    const reached: readonly string[] = [...new Set(moduleSpecifiers(seam))].toSorted();
    const expected: readonly string[] = [
      ...(rawSurface(owner).length > 0 ? [SDK_ENTRY] : []),
      ...(generatedSurface(owner).length > 0 ? [QUERY_ENTRY] : []),
    ].toSorted();

    expect(code, `${seam} imports something`).not.toMatch(/^\s*import\s/mu);
    expect(code, `${seam} re-exports a whole entry`).not.toMatch(/export\s*\*/u);
    expect(reached).toEqual(expected);
  });
});

describe("every data consumer reaches the API through its seam", () => {
  it.each(Object.entries(DATA_CONSUMERS))(
    "%s takes its data artifacts from its feature's seam",
    (path: string, consumer: DataConsumer) => {
      // Per NAME, not per specifier: a rewrite that keeps the seam import but
      // drops one of two artifacts still compiles, because TypeScript resolves
      // the survivor, and leaves a screen that can start a flow but not finish it.
      expect(importedNamesFrom(path, consumer.seam)).toEqual(
        expect.arrayContaining([...consumer.names]),
      );
    },
  );

  it("leaves no import of the generated query entry anywhere under features/ or routes/", () => {
    // Named as a list, so a failure reports WHICH module reached around the seam.
    // This is the criterion the bead states, and the one nothing else enforces:
    // the lint rule that will subsume it belongs to a later feature of this epic.
    expect(
      boundaryScope().filter((path): boolean => moduleSpecifiers(path).includes(QUERY_ENTRY)),
    ).toEqual([]);
  });

  it("leaves no import of a raw SDK operation anywhere under features/ or routes/", () => {
    // Per NAME rather than per specifier, because most of these files keep
    // importing the raw barrel for a return-url guard or a DTO type — a
    // specifier-level ban would demand those become collateral damage.
    const offenders: readonly string[] = boundaryScope().flatMap((path): readonly string[] =>
      importedNamesFrom(path, SDK_ENTRY)
        .filter((name): boolean => RAW_DATA_OPERATIONS.has(name))
        .map((name): string => `${path} -> ${name}`),
    );

    expect(offenders.toSorted()).toEqual([]);
  });

  it("still lets the pure helpers and DTO types come straight from the raw barrel", () => {
    // The anti-overreach pole. `isSafeReturnUrl` issues no request, so it is not
    // the seam's business; if this ever reports zero files, the sweep has pulled
    // the whole barrel behind the seams and each api.ts has stopped being a
    // readable list of the feature's endpoints.
    const users: readonly string[] = boundaryScope().filter((path): boolean =>
      importedNamesFrom(path, SDK_ENTRY).includes(PURE_HELPER),
    );

    expect(
      users.length,
      `no module imports ${PURE_HELPER} from ${SDK_ENTRY} any more`,
    ).toBeGreaterThan(1);
  });
});

describe("the feature dirs that reach no endpoint", () => {
  it.each(NO_SEAM_FEATURES)("features/%s exists and is genuinely data-free", (feature: string) => {
    // Checked rather than assumed, per the bead: these five arrived after the
    // plan was written, and a feature that has quietly grown a data call needs a
    // seam like every other.
    expect(existsSync(join(srcDir, "features", feature))).toBe(true);
    expect(seamOwners(), `features/${feature} is in the seam table`).not.toContain(feature);
  });

  it.each(NO_SEAM_FEATURES)("features/%s is given no empty seam", (feature: string) => {
    const seam: string = seamPath(feature);
    const spec = `features/${feature}/api.test.ts`;

    expect(existsSync(join(srcDir, seam)), `${seam} should not exist`).toBe(false);
    expect(existsSync(join(srcDir, spec)), `${spec} should not exist`).toBe(false);
  });
});

/**
 * The split between the two entries, resolved rather than declared.
 *
 * Everything above takes {@link RAW_DATA_OPERATIONS} on trust: it is a hand-written
 * set, and a name wrongly in it would send a seam to the raw barrel for something
 * the generator does emit — re-introducing exactly the hand-rolled request shape
 * Wallow-x4qn.9.3 removed, behind a seam that now hides it. These cases prove the
 * partition against the built package.
 */
describe("the SDK entries behind the seams", () => {
  const generated: readonly string[] = seamOwners().flatMap((owner): readonly string[] =>
    generatedSurface(owner),
  );

  it.each([...new Set(generated)].toSorted())(
    "%s is a generated artifact on the query entry",
    async (name: string) => {
      const query: Record<string, unknown> = await importSdkEntry("./query");

      expect(typeof query[name], `${QUERY_ENTRY} exports no ${name}`).toBe("function");
    },
  );

  it.each([...RAW_DATA_OPERATIONS].toSorted())(
    "%s is a request function on the raw barrel and is not itself on the query entry",
    async (name: string) => {
      // Only the BARE name. The generated `{op}Mutation()` factory for each of these
      // three does exist — the generator emits one for every non-GET operation — and
      // that the feature deliberately does not adopt it is asserted in the feature's
      // own `api.test.ts`, where the rationale lives.
      const [sdk, query]: readonly Record<string, unknown>[] = await Promise.all([
        importSdkEntry("."),
        importSdkEntry("./query"),
      ]);

      expect(typeof sdk[name], `${SDK_ENTRY} exports no ${name}`).toBe("function");
      expect(
        name in query,
        `${QUERY_ENTRY} exports ${name} itself — the seam should take it from there`,
      ).toBe(false);
    },
  );

  it("keeps the pure helper off the query entry, where a seam would never find it", async () => {
    // Why `isSafeReturnUrl` cannot be swept into a seam even by accident: it is
    // not on the generated entry at all, so the only way it reaches an api.ts is
    // a deliberate re-export from the barrel.
    const query: Record<string, unknown> = await importSdkEntry("./query");

    expect(PURE_HELPER in query).toBe(false);
  });
});
