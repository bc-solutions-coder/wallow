import { execFileSync } from "node:child_process";
import {
  copyFileSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  realpathSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, describe, expect, it } from "vitest";

/**
 * Import guardrails for the collapsed SDK surface (bead Wallow-pu6a.5.8).
 *
 * Beads 5.2-5.5 deleted the module-global client, the hand-written query slices
 * and key registry, and the three per-app facade singletons. Deleting them stops
 * TODAY's call sites; it does not stop the pattern coming back, because the
 * shapes are all still writable — a fork can hand-roll a `queryKeys` object or
 * reach past the exports map into `dist/` and get a working build with none of
 * the per-request isolation the collapse bought. `no-restricted-imports` is what
 * makes that a build failure with a message naming the replacement.
 *
 * A config assertion alone would pass against a rule that silently matches
 * nothing, so the config surface is pinned AND the real binary is run over
 * snippets: each restricted form must produce a `no-restricted-imports`
 * diagnostic, and the surviving entry points must produce none.
 *
 * The second half of the file covers the facade ban (`@tanstack/react-query` is
 * reachable only through `@bc-solutions-coder/query`) and its exemption, which
 * needs a stronger harness than a snippet — see the comment above
 * `lintMirrorTree`.
 */

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
// packages/sdk -> repo root
const repoRoot: string = resolve(packageRoot, "..", "..");
const oxlintConfigPath: string = resolve(repoRoot, ".oxlintrc.json");
const oxlintBinPath: string = resolve(repoRoot, "node_modules", ".bin", "oxlint");

const RULE_CODE: string = "eslint(no-restricted-imports)";

interface RestrictedPath {
  name: string;
  importNames?: string[];
  message?: string;
}

interface RestrictedPattern {
  group: string[];
  message?: string;
}

interface RestrictedImportsOptions {
  paths?: RestrictedPath[];
  patterns?: RestrictedPattern[];
}

interface OxlintOverride {
  files?: string[];
  rules?: Record<string, unknown>;
}

interface OxlintConfig {
  rules?: Record<string, unknown>;
  overrides?: OxlintOverride[];
}

function readConfig(): OxlintConfig {
  return JSON.parse(readFileSync(oxlintConfigPath, "utf8")) as OxlintConfig;
}

function readRuleEntry(): [string, RestrictedImportsOptions] {
  const entry: unknown = readConfig().rules?.["no-restricted-imports"];

  expect(Array.isArray(entry)).toBe(true);
  const [severity, options] = entry as [string, RestrictedImportsOptions];
  return [severity, options];
}

/** Every `overrides[]` entry that says anything about `no-restricted-imports`. */
function overridesTouchingTheRule(): OxlintOverride[] {
  return (readConfig().overrides ?? []).filter(
    (override: OxlintOverride): boolean => override.rules?.["no-restricted-imports"] !== undefined,
  );
}

/** Every override that RE-DECLARES the rule (a `[severity, options]` array). */
function overridesRedeclaringTheRule(): OxlintOverride[] {
  return overridesTouchingTheRule().filter((override: OxlintOverride): boolean =>
    Array.isArray(override.rules?.["no-restricted-imports"]),
  );
}

/** The globs the facade exemption covers: the facade package and its four SDK peers. */
const FACADE_EXEMPTION_FILES: readonly string[] = [
  "packages/query/**",
  "packages/sdk/src/generated-query-surface.test.ts",
  "packages/sdk/src/query/invalidations.ts",
  "packages/sdk/src/route-context.test.ts",
  "packages/sdk/src/route-context.ts",
];

/** The globs the zero-dependency packages' shared charter override covers. */
const ZERO_DEP_CHARTER_FILES: readonly string[] = [
  "packages/env/src/**/*.ts",
  "packages/logger/src/**/*.ts",
  "packages/utils/src/**/*.ts",
];

/**
 * The override that RE-DECLARES the rule for the facade and its four SDK peers,
 * instead of switching it off.
 *
 * oxlint has no per-name partial disable, so the only way to let those five
 * import the real package while every other ban survives is to copy the rule
 * minus one entry.
 */
function facadeExemption(): [OxlintOverride, RestrictedImportsOptions] {
  const match: OxlintOverride | undefined = overridesRedeclaringTheRule().find(
    (override: OxlintOverride): boolean =>
      (override.files ?? []).includes(FACADE_EXEMPTION_FILES[0] ?? ""),
  );

  expect(match, "no override re-declares no-restricted-imports for the facade").toBeDefined();

  const override: OxlintOverride = match ?? {};
  const [, options] = (override.rules?.["no-restricted-imports"] ?? ["error", {}]) as [
    string,
    RestrictedImportsOptions,
  ];

  return [override, options];
}

function pathNames(options: RestrictedImportsOptions): string[] {
  return (options.paths ?? []).map((path: RestrictedPath): string => path.name);
}

function restrictedPathFor(moduleName: string): RestrictedPath {
  const [, options] = readRuleEntry();
  const match: RestrictedPath | undefined = (options.paths ?? []).find(
    (candidate: RestrictedPath): boolean => candidate.name === moduleName,
  );

  expect(match, `no-restricted-imports has no entry for ${moduleName}`).toBeDefined();
  return match ?? { name: moduleName };
}

function allPatternGroups(): string[] {
  const [, options] = readRuleEntry();
  return (options.patterns ?? []).flatMap((pattern: RestrictedPattern): string[] => pattern.group);
}

const scratchDir: string = mkdtempSync(join(tmpdir(), "wallow-oxlint-guardrails-"));

afterAll((): void => {
  rmSync(scratchDir, { force: true, recursive: true });
});

/**
 * Lints `source` with the repo's real config and returns only the
 * `no-restricted-imports` diagnostics, so an unrelated stylistic warning in a
 * snippet cannot be mistaken for the guardrail firing (or for it staying quiet).
 */
function restrictedImportDiagnostics(fileName: string, source: string): string[] {
  const filePath: string = join(scratchDir, fileName);
  writeFileSync(filePath, source, "utf8");

  let stdout: string = "";
  try {
    stdout = execFileSync(oxlintBinPath, ["-c", oxlintConfigPath, "-f", "json", filePath], {
      cwd: repoRoot,
      encoding: "utf8",
      stdio: "pipe",
    });
  } catch (error: unknown) {
    stdout = (error as { stdout?: string }).stdout ?? "";
  }

  const report: { diagnostics?: { code?: string; message?: string }[] } = JSON.parse(stdout) as {
    diagnostics?: { code?: string; message?: string }[];
  };

  return (report.diagnostics ?? [])
    .filter((diagnostic): boolean => diagnostic.code === RULE_CODE)
    .map((diagnostic): string => diagnostic.message ?? "");
}

describe("the root oxlint config restricts the deleted SDK surface", () => {
  it("registers no-restricted-imports as an error, not a warning", () => {
    const [severity] = readRuleEntry();
    expect(severity).toBe("error");
  });

  it.each([
    "client",
    "configureBffClient",
    "configureSsrClient",
    "configureWallowClient",
    "createAuthClient",
    "createConfiguredOnce",
    "createMfaClient",
    "getSsrRequestContext",
    "resolveSsrFetchOrigin",
    "setSsrRequestContextResolver",
    "unwrap",
    "wireSsrCookieInterceptor",
  ])("restricts the deleted browser-entry export %s", (exportName: string) => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk").importNames).toContain(exportName);
  });

  it.each([
    "appsQueries",
    "authQueries",
    "ensureQueryBootstrapped",
    "inquiriesQueries",
    "mfaQueries",
    "organizationsQueries",
    "queryKeys",
    "registerQueryBootstrap",
    "resetQueryBootstrapForTests",
    "settingsQueries",
    "userQueries",
  ])("restricts the retired query-entry export %s", (exportName: string) => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk/query").importNames).toContain(exportName);
  });

  it.each([
    "@bc-solutions-coder/sdk/dist/**",
    "@bc-solutions-coder/sdk/src/**",
    "@bc-solutions-coder/sdk/generated/**",
  ])("restricts deep imports matching %s", (group: string) => {
    expect(allPatternGroups()).toContain(group);
  });

  it.each(["**/lib/wallow-sdk", "**/lib/wallow-auth-sdk", "**/lib/sdk"])(
    "restricts the deleted app facade path %s",
    (group: string) => {
      expect(allPatternGroups()).toContain(group);
    },
  );

  it("points a reader at the per-request factory rather than only forbidding", () => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk").message).toContain("createWallowSdk");
  });

  it("points a reader at the generated query artifacts", () => {
    expect(restrictedPathFor("@bc-solutions-coder/sdk/query").message).toMatch(/Options\(\)/u);
  });
});

describe("the guardrails fire on real source", () => {
  it.each([
    [
      "singleton-config.ts",
      'import { configureBffClient } from "@bc-solutions-coder/sdk";\nexport const use = configureBffClient;\n',
    ],
    [
      "ssr-resolver.ts",
      'import { setSsrRequestContextResolver } from "@bc-solutions-coder/sdk";\nexport const use = setSsrRequestContextResolver;\n',
    ],
    [
      "unwrap.ts",
      'import { unwrap } from "@bc-solutions-coder/sdk";\nexport const use = unwrap;\n',
    ],
    [
      "key-registry.ts",
      'import { queryKeys } from "@bc-solutions-coder/sdk/query";\nexport const use = queryKeys;\n',
    ],
    [
      "query-slice.ts",
      'import { organizationsQueries } from "@bc-solutions-coder/sdk/query";\nexport const use = organizationsQueries;\n',
    ],
    [
      "deep-dist.ts",
      'import { thing } from "@bc-solutions-coder/sdk/dist/index.js";\nexport const use = thing;\n',
    ],
    [
      "deep-src.ts",
      'import { thing } from "@bc-solutions-coder/sdk/src/generated/sdk.gen";\nexport const use = thing;\n',
    ],
    [
      "app-facade.ts",
      'import { getSdk } from "../../lib/wallow-auth-sdk";\nexport const use = getSdk;\n',
    ],
  ])("rejects %s", (fileName: string, source: string) => {
    expect(restrictedImportDiagnostics(fileName, source)).not.toHaveLength(0);
  });

  // A guardrail that also catches `export ... from` matters more than it looks:
  // the surviving `features/<name>/api.ts` seams are pure re-export files, so a
  // rule blind to that form would miss the most likely place a deleted symbol
  // creeps back in.
  it("rejects a re-export of a deleted symbol, not only a plain import", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "reexport-seam.ts",
      'export { configureBffClient } from "@bc-solutions-coder/sdk";\n',
    );

    expect(diagnostics).not.toHaveLength(0);
  });

  it("rejects a type-only deep import", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "type-only-deep.ts",
      'import type { Thing } from "@bc-solutions-coder/sdk/dist/index.js";\nexport type Alias = Thing;\n',
    );

    expect(diagnostics).not.toHaveLength(0);
  });

  it("leaves the surviving entry points alone", () => {
    const diagnostics: string[] = restrictedImportDiagnostics(
      "compliant.ts",
      [
        'import { createWallowSdk } from "@bc-solutions-coder/sdk";',
        'import { createApiPassthrough } from "@bc-solutions-coder/sdk/server/passthrough";',
        'import { createWallowBffServer } from "@bc-solutions-coder/sdk/server";',
        'import { organizationsGetAllOptions, queriesWithTag } from "@bc-solutions-coder/sdk/query";',
        "",
        "export const surface = [",
        "  createWallowSdk,",
        "  createApiPassthrough,",
        "  createWallowBffServer,",
        "  organizationsGetAllOptions,",
        "  queriesWithTag,",
        "];",
        "",
      ].join("\n"),
    );

    expect(diagnostics).toEqual([]);
  });
});

/**
 * The facade ban (bead Wallow-x4qn.12) — `@tanstack/react-query` is importable
 * only through `@bc-solutions-coder/query`, and `@bc-solutions-coder/web-shell`
 * is importable nowhere.
 *
 * This ban is the first one in the config with an EXEMPTION, and an exemption is
 * the part that rots: oxlint cannot disable one restricted name, so the facade
 * and the four SDK files that hold the optional peer are let through by a
 * re-declared copy of the rule. Two things can quietly go wrong there and
 * neither shows up as a lint error — the exemption's glob can be wider than the
 * five locations it names (reopening the ban for a whole package), and the
 * re-declared copy can drift into `"off"` (reopening every OTHER ban for those
 * files). Snippets linted from a scratch directory cannot see either, because an
 * absolute path outside the repo matches no `files` glob at all.
 *
 * So the fixtures below are a MIRROR of the repo: a tmp root holding a verbatim
 * copy of `.oxlintrc.json` plus files at the repo-relative paths that matter.
 * oxlint resolves `files` and `ignorePatterns` against the config's own
 * directory, so a fixture at `apps/wallow-web/src/routes/dashboard.tsx` is
 * matched by exactly the globs its real counterpart is — which makes "an app
 * violation is caught" and "the exemption stops at these five locations"
 * assertions about the real binary's behaviour rather than about config text.
 */

/** One `no-restricted-imports` diagnostic, attributed to the mirror file that earned it. */
interface Offence {
  readonly file: string;
  readonly message: string;
  readonly help: string;
}

/**
 * `realpathSync` is load-bearing, not tidiness: oxlint canonicalizes each file's
 * path before matching it against an override's `files`, while the globs are
 * resolved against the config's directory as given. On macOS `os.tmpdir()` is
 * `/var/folders/...`, a symlink to `/private/var/folders/...`, so a mirror rooted
 * at the symlinked form matches NO override — every exemption silently evaporates
 * and the tree only ever proves the root rule. The harness proof below is what
 * catches that.
 */
const mirrorParent: string = mkdtempSync(join(tmpdir(), "wallow-oxlint-mirror-"));
const mirrorRoot: string = realpathSync(mirrorParent);

afterAll((): void => {
  rmSync(mirrorRoot, { force: true, recursive: true });
});

/**
 * Lints a tree of repo-relative fixture files against a copy of the real config
 * placed at the tree's root, and returns every `no-restricted-imports`
 * diagnostic it produced. One binary run covers the whole fixture set.
 */
function lintMirrorTree(files: Readonly<Record<string, string>>): Offence[] {
  copyFileSync(oxlintConfigPath, join(mirrorRoot, ".oxlintrc.json"));

  for (const [relativePath, source] of Object.entries(files)) {
    const full: string = join(mirrorRoot, relativePath);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, source, "utf8");
  }

  let stdout: string = "";
  try {
    stdout = execFileSync(
      oxlintBinPath,
      ["-c", join(mirrorRoot, ".oxlintrc.json"), "-f", "json", "."],
      { cwd: mirrorRoot, encoding: "utf8", stdio: "pipe" },
    );
  } catch (error: unknown) {
    stdout = (error as { stdout?: string }).stdout ?? "";
  }

  const report: {
    diagnostics?: { code?: string; filename?: string; help?: string; message?: string }[];
  } = JSON.parse(stdout) as {
    diagnostics?: { code?: string; filename?: string; help?: string; message?: string }[];
  };

  return (report.diagnostics ?? [])
    .filter((diagnostic): boolean => diagnostic.code === RULE_CODE)
    .map(
      (diagnostic): Offence => ({
        // oxlint reports paths relative to the directory it was pointed at.
        file: (diagnostic.filename ?? "").replace(/^\.\//u, ""),
        help: diagnostic.help ?? "",
        message: diagnostic.message ?? "",
      }),
    );
}

const FACADE = "@bc-solutions-coder/query";
const TANSTACK = "@tanstack/react-query";
const WEB_SHELL = "@bc-solutions-coder/web-shell";

/**
 * Every fixture, linted in one pass. Each path is one a real file could occupy,
 * so the glob that matches it here is the glob that would match it in the repo.
 */
const MIRROR_FIXTURES: Readonly<Record<string, string>> = {
  // The harness's own proof, using an exemption that already exists: the seam
  // specs turn the rule off, their siblings do not. Linted before anything is
  // claimed about the new ban, so a green result below is evidence of the config
  // rather than of a mirror where no override ever matches.
  "apps/wallow-web/src/features/apps/api.test.ts":
    'import { queryKeys } from "@bc-solutions-coder/sdk/query";\nexport const use = queryKeys;\n',
  "apps/wallow-web/src/features/apps/list.ts":
    'import { queryKeys } from "@bc-solutions-coder/sdk/query";\nexport const use = queryKeys;\n',

  // Consumers that must all go through the facade.
  "apps/wallow-web/src/routes/dashboard/index.tsx":
    'import { useQuery } from "@tanstack/react-query";\nexport const use = useQuery;\n',
  "apps/wallow-auth/src/features/login/use-login.ts":
    'import { useMutation } from "@tanstack/react-query";\nexport const use = useMutation;\n',
  "apps/examples/minimal-app/src/main.tsx":
    'import { QueryClientProvider } from "@tanstack/react-query";\nexport const use = QueryClientProvider;\n',
  // packages/forms is routed through the facade by an earlier feature, so the ban
  // applies to it like any other package — it gets NO exemption. Type-only, which
  // is the form a "harmless" direct import comes back as.
  "packages/forms/src/core/use-app-form.ts":
    'import type { UseMutationResult } from "@tanstack/react-query";\nexport type Result = UseMutationResult;\n',
  "packages/auth/src/current-user.ts":
    'import { useQuery } from "@tanstack/react-query";\nexport const use = useQuery;\n',
  "packages/testing/src/render.tsx":
    'import { QueryClient } from "@tanstack/react-query";\nexport const use = QueryClient;\n',
  // Inside the SDK but not one of the four named files: the exemption must be
  // per-file, not `packages/sdk/**`.
  "packages/sdk/src/create-sdk.ts": 'export { useQuery } from "@tanstack/react-query";\n',
  // Inside the SDK's query directory but not invalidations.ts.
  "packages/sdk/src/query/index.ts":
    'import { useQuery } from "@tanstack/react-query";\nexport const use = useQuery;\n',

  // The deleted package, which nothing may import — app or facade.
  "apps/wallow-web/src/router.tsx":
    'import { createQueryClient } from "@bc-solutions-coder/web-shell";\nexport const use = createQueryClient;\n',

  // The exemption's five locations. Each also carries a violation of a ban that
  // must SURVIVE the re-declaration, so a drift to `"off"` fails here.
  "packages/query/src/index.ts": [
    'export * from "@tanstack/react-query";',
    'import { configureBffClient } from "@bc-solutions-coder/sdk";',
    "export const legacy = configureBffClient;",
    "",
  ].join("\n"),
  "packages/query/src/query-client.ts": [
    'import { QueryClient } from "@tanstack/react-query";',
    'import { createQueryClient } from "@bc-solutions-coder/web-shell";',
    "export const surface = [QueryClient, createQueryClient];",
    "",
  ].join("\n"),
  "packages/sdk/src/route-context.ts": [
    'import type { QueryClient } from "@tanstack/react-query";',
    'import { queryKeys } from "@bc-solutions-coder/sdk/query";',
    "export const legacy = queryKeys;",
    "export type Client = QueryClient;",
    "",
  ].join("\n"),
  "packages/sdk/src/route-context.test.ts": [
    'import { QueryClient } from "@tanstack/react-query";',
    'import { thing } from "@bc-solutions-coder/sdk/dist/index.js";',
    "export const surface = [QueryClient, thing];",
    "",
  ].join("\n"),
  "packages/sdk/src/query/invalidations.ts":
    'import type { QueryKey } from "@tanstack/react-query";\nexport type Key = QueryKey;\n',
  "packages/sdk/src/generated-query-surface.test.ts":
    'import { QueryClient } from "@tanstack/react-query";\nexport const use = QueryClient;\n',

  // The compliant shape every consumer is expected to write.
  "apps/wallow-web/src/routes/settings.tsx": [
    'import { createQueryClient, useMutation, useQuery } from "@bc-solutions-coder/query";',
    "export const surface = [createQueryClient, useMutation, useQuery];",
    "",
  ].join("\n"),
};

const mirrorOffences: Offence[] = lintMirrorTree(MIRROR_FIXTURES);

/** Every offence the mirror reported for one file. */
function offencesIn(file: string): Offence[] {
  return mirrorOffences.filter((offence: Offence): boolean => offence.file === file);
}

/** Every offence for one file that names a given module — the ban that fired. */
function offencesNaming(file: string, moduleName: string): string[] {
  return offencesIn(file)
    .filter((offence: Offence): boolean => offence.message.includes(`'${moduleName}`))
    .map((offence: Offence): string => `${offence.message} ${offence.help}`);
}

describe("the mirror tree reproduces the repo's override matching", () => {
  it("finds fixtures to lint", () => {
    // Without this the per-file assertions could go vacuously green: a mirror
    // that oxlint silently skipped reports no offences and forbids nothing.
    expect(mirrorOffences.length).toBeGreaterThan(0);
  });

  it("suppresses a ban inside a glob the real config exempts", () => {
    expect(offencesIn("apps/wallow-web/src/features/apps/api.test.ts")).toEqual([]);
  });

  it("still reports it one file over, outside that glob", () => {
    expect(offencesIn("apps/wallow-web/src/features/apps/list.ts")).not.toHaveLength(0);
  });
});

describe("only the facade may import @tanstack/react-query", () => {
  it.each([
    "apps/wallow-web/src/routes/dashboard/index.tsx",
    "apps/wallow-auth/src/features/login/use-login.ts",
    "apps/examples/minimal-app/src/main.tsx",
    "packages/forms/src/core/use-app-form.ts",
    "packages/auth/src/current-user.ts",
    "packages/testing/src/render.tsx",
    "packages/sdk/src/create-sdk.ts",
    "packages/sdk/src/query/index.ts",
  ])("rejects the direct import in %s", (file: string) => {
    expect(offencesNaming(file, TANSTACK)).not.toHaveLength(0);
  });

  it("names the facade as the replacement rather than only forbidding", () => {
    expect(offencesNaming("apps/wallow-web/src/routes/dashboard/index.tsx", TANSTACK)[0]).toContain(
      FACADE,
    );
  });

  it.each([
    "packages/query/src/index.ts",
    "packages/query/src/query-client.ts",
    "packages/sdk/src/route-context.ts",
    "packages/sdk/src/route-context.test.ts",
    "packages/sdk/src/query/invalidations.ts",
    "packages/sdk/src/generated-query-surface.test.ts",
  ])("allows the direct import in %s", (file: string) => {
    expect(offencesNaming(file, TANSTACK)).toEqual([]);
  });

  it("leaves a consumer that imports through the facade completely alone", () => {
    expect(offencesIn("apps/wallow-web/src/routes/settings.tsx")).toEqual([]);
  });
});

describe("nothing may import the deleted @bc-solutions-coder/web-shell", () => {
  it("rejects it in an app", () => {
    expect(offencesNaming("apps/wallow-web/src/router.tsx", WEB_SHELL)).not.toHaveLength(0);
  });

  it("points at the facade's createQueryClient instead", () => {
    const [message] = offencesNaming("apps/wallow-web/src/router.tsx", WEB_SHELL);

    expect(message).toContain(FACADE);
    expect(message).toContain("createQueryClient");
  });

  it("rejects it inside the exemption too", () => {
    expect(offencesNaming("packages/query/src/query-client.ts", WEB_SHELL)).not.toHaveLength(0);
  });
});

describe("the exemption reopens one ban, not the rest", () => {
  it("still restricts a deleted browser-entry export inside the facade", () => {
    expect(offencesNaming("packages/query/src/index.ts", "configureBffClient")).not.toHaveLength(0);
  });

  it("still restricts a retired query export inside an exempted SDK file", () => {
    expect(offencesNaming("packages/sdk/src/route-context.ts", "queryKeys")).not.toHaveLength(0);
  });

  it("still restricts a dist deep import inside an exempted SDK file", () => {
    expect(
      offencesNaming("packages/sdk/src/route-context.test.ts", "@bc-solutions-coder/sdk/dist"),
    ).not.toHaveLength(0);
  });
});

describe("the root config states the facade convention", () => {
  it("bans the whole @tanstack/react-query package, not a list of its exports", () => {
    // With `importNames` the ban would cover only the symbols named today, and
    // the next hook react-query adds would be importable directly.
    expect(restrictedPathFor(TANSTACK).importNames).toBeUndefined();
  });

  it("points a reader at the facade, and says why it is the only door", () => {
    const message: string = restrictedPathFor(TANSTACK).message ?? "";

    expect(message).toContain(FACADE);
    expect(message).toMatch(/version/iu);
    expect(message).toContain("QueryClient");
  });

  it("bans the deleted web-shell package by name", () => {
    const message: string = restrictedPathFor(WEB_SHELL).message ?? "";

    expect(message).toContain("createQueryClient");
    expect(message).toContain(FACADE);
  });
});

describe("the facade exemption is narrow by construction", () => {
  it("re-declares the rule in exactly two places, both named here", () => {
    // Re-declaring is the only way to change what the rule bans for a subtree,
    // and it REPLACES the root options rather than merging — so an unlisted
    // re-declaration silently unbans everything it forgot to restate. The
    // legitimate two are the facade exemption (drops one ban) and the
    // zero-dependency packages' shared charter (adds three); anything else is a
    // regression.
    expect(
      overridesRedeclaringTheRule().map(
        (override: OxlintOverride): Set<string> => new Set(override.files),
      ),
    ).toEqual([new Set(FACADE_EXEMPTION_FILES), new Set(ZERO_DEP_CHARTER_FILES)]);
  });

  it("covers the facade package and exactly the four SDK peers", () => {
    const [override] = facadeExemption();

    // Compared as a set, and exactly — a sixth glob here is how the exemption
    // grows from "the facade plus its four peers" into "whatever was failing".
    expect(new Set(override.files)).toEqual(new Set(FACADE_EXEMPTION_FILES));
  });

  it("omits @tanstack/react-query from its re-declared paths", () => {
    const [, options] = facadeExemption();

    expect(pathNames(options)).not.toContain(TANSTACK);
  });

  it("carries the root rule's other restricted paths verbatim", () => {
    const [, rootOptions] = readRuleEntry();
    const [, options] = facadeExemption();
    const survivors: RestrictedPath[] = (rootOptions.paths ?? []).filter(
      (path: RestrictedPath): boolean => path.name !== TANSTACK,
    );

    expect(options.paths).toEqual(survivors);
  });

  it("carries the root rule's restricted patterns verbatim", () => {
    const [, rootOptions] = readRuleEntry();
    const [, options] = facadeExemption();

    expect(options.patterns).toEqual(rootOptions.patterns);
  });

  it("turns the rule off only for the two seam-spec globs", () => {
    // A `"no-restricted-imports": "off"` over `packages/sdk/**` would be the easy
    // way to write this exemption and would reopen every ban above for the whole
    // package. The seam specs are the one place the rule is legitimately off.
    const disabled: string[] = overridesTouchingTheRule()
      .filter(
        (override: OxlintOverride): boolean => override.rules?.["no-restricted-imports"] === "off",
      )
      .flatMap((override: OxlintOverride): string[] => override.files ?? []);

    expect(disabled).toHaveLength(2);
    expect(new Set(disabled)).toEqual(
      new Set(["apps/**/src/features/*/api.test.ts", "packages/sdk/src/**/index.test.ts"]),
    );
  });
});

/**
 * Build output and dependency trees, skipped so the walk below stays fast and
 * never mistakes a dependency's own config for one of ours. Dot-prefixed
 * directories go too (`.git`, `.docfx`, `.output`, `.nitro`, `.tanstack`): no
 * authored `.oxlintrc.json` lives inside one, and `.docfx` alone holds hundreds
 * of generated files.
 */
const UNWALKED_DIRECTORIES: Set<string> = new Set([
  "bin",
  "coverage",
  "dist",
  "node_modules",
  "obj",
  "playwright-report",
  "test-results",
]);

/** Every authored `.oxlintrc.json` in the tree, root included. */
function oxlintConfigPaths(dir: string = repoRoot): string[] {
  const found: string[] = [];

  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full: string = join(dir, entry.name);
    const walkable: boolean =
      entry.isDirectory() && !entry.name.startsWith(".") && !UNWALKED_DIRECTORIES.has(entry.name);

    if (walkable) {
      found.push(...oxlintConfigPaths(full));
    } else if (entry.isFile() && entry.name === ".oxlintrc.json") {
      found.push(full);
    }
  }

  return found;
}

interface NestedOxlintConfig extends OxlintConfig {
  extends?: string[];
  categories?: Record<string, string>;
  plugins?: string[];
}

/**
 * oxlint reads its config as JSONC, and the nested configs use that: each
 * `jsPlugins` entry and each scoped `wallow/*` exemption carries the reason it
 * exists as a `//` comment beside it, which is the only place a reader looking
 * at the override will find it. `JSON.parse` rejects those, so strip line
 * comments first — the same treatment `tsconfig.json` gets elsewhere.
 */
function readNestedConfig(configPath: string): NestedOxlintConfig {
  const text: string = readFileSync(configPath, "utf8").replaceAll(/^\s*\/\/.*$/gmu, "");

  return JSON.parse(text) as NestedOxlintConfig;
}

describe("every nested oxlint config inherits the root", () => {
  const nested: string[] = oxlintConfigPaths().filter(
    (configPath: string): boolean => configPath !== oxlintConfigPath,
  );

  it("finds the nested configs to check", () => {
    // Non-vacuity guard: a broken walk would make every assertion below pass by
    // iterating an empty list, which is precisely the failure mode this whole
    // block exists to close (bead Wallow-i3hr).
    expect(nested.length).toBeGreaterThanOrEqual(3);
  });

  it.each(nested.map((configPath: string): string => relative(repoRoot, configPath)))(
    "%s extends the root config",
    (relativePath: string) => {
      // oxlint reads the NEAREST config for a file and does not merge upward on
      // its own, so a nested config without `extends` silently replaces the
      // root's plugins, categories and every no-restricted-imports ban for its
      // whole subtree — the package looks linted while running almost no rules.
      const configPath: string = resolve(repoRoot, relativePath);
      const resolved: string[] = (readNestedConfig(configPath).extends ?? []).map(
        (entry: string): string => resolve(dirname(configPath), entry),
      );

      expect(resolved).toContain(oxlintConfigPath);
    },
  );

  it.each(nested.map((configPath: string): string => relative(repoRoot, configPath)))(
    "%s does not redeclare the severity baseline",
    (relativePath: string) => {
      // `categories` and `plugins` in a nested config detach it from the root's
      // baseline even WITH `extends`, which reintroduces the same blind spot one
      // level down. A nested config narrows named rules; it never restates the
      // baseline.
      const config: NestedOxlintConfig = readNestedConfig(resolve(repoRoot, relativePath));

      expect(config.categories).toBeUndefined();
      expect(config.plugins).toBeUndefined();
    },
  );

  it.each(nested.map((configPath: string): string => relative(repoRoot, configPath)))(
    "%s keeps its override globs directory-relative",
    (relativePath: string) => {
      // The non-obvious half. An override glob in a nested config is matched
      // against the path RELATIVE TO THAT CONFIG'S DIRECTORY, so a repo-rooted
      // prefix copied from the root config (`packages/ui/**/*.tsx`) matches
      // nothing and fails silently — the override reads as applied while every
      // rule it names stays on. Ban the prefixes outright.
      const globs: string[] = (readNestedConfig(resolve(repoRoot, relativePath)).overrides ?? [])
        .flatMap((override: OxlintOverride): string[] => override.files ?? [])
        .filter((glob: string): boolean => /^(?:apps|packages|scripts)\//u.test(glob));

      expect(globs).toEqual([]);
    },
  );
});
