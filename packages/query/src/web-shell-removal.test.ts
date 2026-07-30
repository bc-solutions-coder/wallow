/**
 * `@bc-solutions-coder/web-shell` is deleted, and stays deleted (Wallow-x4qn.11).
 *
 * The package held exactly one symbol, `createQueryClient`, which now lives in
 * this package — so this is the successor asserting its predecessor is gone. The
 * thing being shipped is an ABSENCE, and an absence needs a lock: nothing in a
 * build fails when a *reference* to a deleted package survives in prose, and the
 * two places where one does fail (a `COPY packages/web-shell` line in an app
 * Dockerfile, a stale `packages/web-shell:` importer in the lockfile) fail
 * minutes into an image build in CI, never in `pnpm check`.
 *
 * Wallow is a fork-first base platform, which is why the prose counts as much as
 * the code. A fork reads the repo-layout table in `CLAUDE.md`, the package table
 * in `docs/development/frontend-setup.md` and the `workspace:*` snippet beside it
 * as instructions. A row naming a package that no longer exists is a broken
 * instruction, and a dangling reference is this deletion's most likely
 * regression precisely because none of it is compiled.
 *
 * Five locks, in the order a regression would arrive:
 *
 *  1. The package directory is gone, no workspace member is named after it, no
 *     manifest declares it, no importer still has it linked, and the lockfile
 *     has no importer entry for it (a lockfile that still lists a package whose
 *     manifest is absent makes `pnpm install --frozen-lockfile` — the first
 *     command in both app Dockerfiles — an error).
 *  2. Nothing RESOLVES it: no file carries an `import`/`export … from`,
 *     side-effect `import`, `import()` or `require()` whose specifier is the
 *     package or a subpath of it. This lock has no exemptions at all — it is
 *     the "no code may reach for it" lock, and it is deliberately blind to who
 *     is asking. It reads statement position, so a violating import written
 *     INSIDE a string literal (which is what a lint-rule fixture is) is not one.
 *  3. No tracked or untracked-but-unignored file NAMES it, prose included —
 *     unless naming it is the file's whole job. Two categories earn that, and
 *     both are shape-based rather than a list of paths, because a list of paths
 *     is exactly what rotted here once already:
 *       • HISTORY, which RECORDS the package: `CHANGELOG.md`, `pnpm-lock.yaml`'s
 *         resolution history, `docs/plans/**` and `docs/audits/**`.
 *       • ENFORCEMENT, which FORBIDS it: an oxlint config, or any spec. A ban
 *         has to name what it bans, and a spec pinning that ban has to quote it.
 *     Enforcement is EARNED per file, never granted by filename: a lint config
 *     is exempt only while every mention it makes is one of its own
 *     `no-restricted-imports` bans (an `ignorePatterns` glob or a stale
 *     `overrides.files` entry naming the deleted package is dead config, and
 *     stays a failure), and a spec only while one of its own `describe`/`it`
 *     titles names the package as forbidden or absent. Delete the enforcement
 *     and leave the mentions, and the mentions start failing again.
 *  4. No app Dockerfile COPYs a `packages/<name>` directory that does not exist.
 *     The per-app `docker-workspace-copies.test.ts` specs assert the forward
 *     direction (every declared workspace dep is copied); this is the reverse,
 *     which is the direction a DELETION breaks.
 *  5. `scripts/check-exports.sh` lists the packages that replaced it, so the
 *     exports-map gate covers the successors rather than silently shrinking.
 *
 * That the repo-root lint config still carries the ban is not asserted here —
 * `packages/sdk/src/oxlint-guardrails.test.ts` owns it and pins it in five
 * places. This file only asserts what the ban's EXISTENCE buys it: the right to
 * name the package.
 *
 * Both sweeps' detectors are proven against a fixture tree first — carrying a
 * real prose violation, a real import, an import that is only a fixture string,
 * one of each allowlisted-history file, an earning and a non-earning lint
 * config, and an earning and a non-earning spec — so a green repo sweep is
 * evidence of compliance rather than of a scanner that finds nothing.
 *
 * Node project: reads files, mounts nothing.
 */
import { execFileSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, describe, expect, it } from "vitest";

// packages/query/src -> repo root (src -> query -> packages -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

/** The deleted package, by directory name and by package name. */
const DIRECTORY = "web-shell";
const PACKAGE_NAME = "@bc-solutions-coder/web-shell";

/**
 * This spec names the forbidden token in its own assertions and fixtures. It is
 * exempt as ENFORCEMENT like any other spec, on the same earned terms — not by
 * being named here. Kept as a constant only because the fixture tree writes a
 * file at this path, and it is the repo-relative form the sweep reports.
 */
const SELF = "packages/query/src/web-shell-removal.test.ts";

/**
 * Files that legitimately keep naming the package because they RECORD it rather
 * than depend on it: the release history, the lockfile's resolution history, and
 * the local plan/audit artifacts (which are session history, not site content).
 */
const HISTORY_PATHS: readonly RegExp[] = [
  /(?:^|\/)CHANGELOG\.md$/u,
  /^pnpm-lock\.yaml$/u,
  /^docs\/plans\//u,
  /^docs\/audits\//u,
];

/** A quoted module specifier naming the package, or any subpath of it. */
const SPECIFIER = `["'\`]${PACKAGE_NAME}(?:/[^"'\`]+)?["'\`]`;

/**
 * The forms in which a file makes the deleted package RESOLVE. Line-anchored
 * wherever the form is a statement, so that a violating import carried as a
 * STRING — an oxlint fixture proving the ban fires — is not mistaken for one:
 * the string's own opening quote lands inside the anchor's exclusion class.
 */
const RESOLVING_FORMS: readonly RegExp[] = [
  // The whole `import`/`export … from` family, including the multi-line form
  // where the specifier lands on a line of its own after a closing brace.
  new RegExp(`^[^"'\`]*\\bfrom\\s+${SPECIFIER}`, "u"),
  // A side-effect import: `import "pkg";`.
  new RegExp(`^\\s*import\\s+${SPECIFIER}`, "u"),
  // Deferred and CJS resolution. A call, so it may sit anywhere on the line.
  new RegExp(`\\b(?:import|require)\\s*\\(\\s*${SPECIFIER}`, "u"),
];

/** Words with which a test title says the package must not be here. */
const PROHIBITION = /ban|forbid|restrict|reject|delet|remov|gone|absent|no longer|nothing/iu;

/** Every `describe`/`it` title a spec declares, however the callback is parameterised. */
const TEST_TITLE = /^\s*(?:describe|it|test)(?:\.\w+)*\(\s*(["'`])([^\n]*?)\1/gmu;

/** How many times a piece of text names the deleted package. */
function mentions(text: string): number {
  return text.split(DIRECTORY).length - 1;
}

/** One entry of oxlint's `no-restricted-imports` `paths` array. */
interface BannedPath {
  readonly name?: string;
  readonly message?: string;
}

interface RestrictedImportsOptions {
  readonly paths?: readonly BannedPath[];
}

interface OxlintOverride {
  readonly rules?: Record<string, unknown>;
}

interface OxlintConfig {
  readonly rules?: Record<string, unknown>;
  readonly overrides?: readonly OxlintOverride[];
}

/**
 * Every `no-restricted-imports` options object the config carries — the root
 * rule plus each override's re-declared copy. An override that turns the rule
 * `"off"` carries no options and contributes nothing.
 */
function restrictedImportOptions(config: OxlintConfig): RestrictedImportsOptions[] {
  return [config.rules, ...(config.overrides ?? []).map((o: OxlintOverride) => o.rules)].flatMap(
    (set: Record<string, unknown> | undefined): RestrictedImportsOptions[] => {
      const entry: unknown = set?.["no-restricted-imports"];

      return Array.isArray(entry)
        ? entry.filter(
            (value: unknown): value is RestrictedImportsOptions =>
              typeof value === "object" && value !== null && "paths" in value,
          )
        : [];
    },
  );
}

/**
 * How many mentions of the package a lint config's own bans ACCOUNT FOR: each
 * `paths` entry naming it contributes its `name` plus every mention inside the
 * `message` that explains the replacement.
 */
function bannedMentions(config: OxlintConfig): number {
  return restrictedImportOptions(config)
    .flatMap((options: RestrictedImportsOptions): readonly BannedPath[] => options.paths ?? [])
    .filter((path: BannedPath): boolean => path.name === PACKAGE_NAME)
    .reduce(
      (total: number, path: BannedPath): number =>
        total + mentions(path.name ?? "") + mentions(path.message ?? ""),
      0,
    );
}

/**
 * A lint config earns the exemption only while EVERY mention it makes is one of
 * its own bans. Anything unaccounted for — an `ignorePatterns` glob, an
 * `overrides.files` entry, a comment — is dead config pointing at a package that
 * no longer exists, which is the regression this lock is for.
 */
function everyMentionIsABan(contents: string): boolean {
  let config: OxlintConfig;

  try {
    config = JSON.parse(contents) as OxlintConfig;
  } catch {
    return false;
  }

  return mentions(contents) === bannedMentions(config);
}

/**
 * A spec earns the exemption only while one of its own titles names the package
 * as forbidden or absent. The file is exempt BECAUSE it enforces, so deleting
 * the enforcement while leaving the mentions puts them back in the sweep.
 */
function assertsTheAbsence(contents: string): boolean {
  return [...contents.matchAll(TEST_TITLE)].some((match: RegExpMatchArray): boolean => {
    const title: string = match[2] as string;

    return title.includes(DIRECTORY) && PROHIBITION.test(title);
  });
}

/** A kind of file whose job is to FORBID the package, and what earns it that standing. */
interface EnforcementSurface {
  readonly shape: RegExp;
  readonly forbids: (contents: string) => boolean;
}

/**
 * Matched by SHAPE, not by path: the config that bans the package and any spec
 * that pins the ban are exempt on their own merits, so the next one to be
 * written needs no edit here. A hardcoded roster of self paths is what made this
 * sweep reject the lint ban it was supposed to be protected by.
 */
const ENFORCEMENT_SURFACES: readonly EnforcementSurface[] = [
  { shape: /(?:^|\/)\.oxlintrc(?:\.[\w-]+)?\.json$/u, forbids: everyMentionIsABan },
  { shape: /\.test\.[cm]?[jt]sx?$/u, forbids: assertsTheAbsence },
];

function isHistory(file: string): boolean {
  return HISTORY_PATHS.some((pattern: RegExp): boolean => pattern.test(file));
}

function isEarnedEnforcement(file: string, contents: string): boolean {
  return ENFORCEMENT_SURFACES.some(
    (surface: EnforcementSurface): boolean => surface.shape.test(file) && surface.forbids(contents),
  );
}

/** The four dependency groups pnpm installs from. */
const DEPENDENCY_GROUPS: readonly string[] = [
  "dependencies",
  "devDependencies",
  "peerDependencies",
  "optionalDependencies",
];

interface Manifest {
  readonly name?: string;
  readonly [group: string]: unknown;
}

function readManifest(path: string): Manifest {
  return JSON.parse(readFileSync(path, "utf8")) as Manifest;
}

/** One surviving mention of the deleted package, with the line that made it. */
interface StaleReference {
  readonly file: string;
  readonly line: number;
}

/**
 * A path this sweep can read as prose: it exists, is a regular file (`git
 * ls-files` reports a nested checkout as one directory entry), and carries no
 * NUL byte. Sniffing for NUL rather than matching an extension list means a new
 * binary asset type cannot make the sweep throw.
 */
function isTextFile(path: string): boolean {
  return existsSync(path) && statSync(path).isFile() && !readFileSync(path).includes(0);
}

/**
 * Every line of the given files that matches `flag`, over the files that name
 * the package at all. Binaries are skipped; `exempt` decides which naming files
 * are allowed to.
 */
function sweep(
  root: string,
  files: readonly string[],
  flag: (line: string) => boolean,
  exempt: (file: string, contents: string) => boolean,
): StaleReference[] {
  const found: StaleReference[] = [];

  for (const file of files) {
    if (isTextFile(join(root, file))) {
      const contents: string = readFileSync(join(root, file), "utf8");

      if (contents.includes(DIRECTORY) && !exempt(file, contents)) {
        contents.split("\n").forEach((text: string, index: number): void => {
          if (flag(text)) {
            found.push({ file, line: index + 1 });
          }
        });
      }
    }
  }

  return found;
}

/**
 * Every line that still names the package, minus the files that RECORD it
 * (history) or FORBID it (earned enforcement).
 */
function staleReferences(root: string, files: readonly string[]): StaleReference[] {
  return sweep(
    root,
    files,
    (line: string): boolean => line.includes(DIRECTORY),
    (file: string, contents: string): boolean =>
      isHistory(file) || isEarnedEnforcement(file, contents),
  );
}

/**
 * Every line that makes the package RESOLVE. No file is exempt: history is
 * allowed to remember the package, and a ban is allowed to name it, but neither
 * is allowed to import it.
 */
function resolvingReferences(root: string, files: readonly string[]): StaleReference[] {
  return sweep(
    root,
    files,
    (line: string): boolean => RESOLVING_FORMS.some((form: RegExp): boolean => form.test(line)),
    (): boolean => false,
  );
}

/**
 * Every file git would show a `grep -r` over a clean checkout: tracked files
 * plus untracked ones that are not ignored. Using git's own view is what keeps
 * `node_modules/`, `dist/` and `.output/` out without hand-maintaining a skip
 * list, and it means the sweep sees a file the moment it is written.
 */
function repoFiles(root: string): string[] {
  return execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard"], {
    cwd: root,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
  })
    .split("\n")
    .filter((file: string): boolean => file.length > 0);
}

/**
 * The workspace members `pnpm-lock.yaml`'s `importers:` block declares. Each is
 * a two-space-indented key holding the path of a directory that must still carry
 * a `package.json`; the block ends at the next top-level key.
 */
function lockfileImporters(): string[] {
  const lockfile: string = readFileSync(resolve(repoRoot, "pnpm-lock.yaml"), "utf8");
  const block: string = lockfile.split(/^importers:$/mu)[1] ?? "";
  const untilNextTopLevelKey: string = block.split(/^\S/mu)[0] ?? "";

  return [...untilNextTopLevelKey.matchAll(/^ {2}(\S+):$/gmu)].map(
    (match: RegExpMatchArray): string => match[1] as string,
  );
}

/** Every `packages/<name>` directory an app's Dockerfile COPYs into its build context. */
function copiedPackageDirectories(dockerfile: string): string[] {
  return [...dockerfile.matchAll(/^COPY\s+packages\/([^/\s]+)/gmu)].map(
    (match: RegExpMatchArray): string => match[1] as string,
  );
}

/** Every workspace app's directory, repo-relative — the `apps/*` and `apps/examples/*` globs. */
function appDirectories(): string[] {
  return repoFiles(repoRoot)
    .filter((file: string): boolean =>
      /^apps\/(?:[^/]+|examples\/[^/]+)\/package\.json$/u.test(file),
    )
    .map((file: string): string => dirname(file))
    .toSorted();
}

/** Every workspace package's directory, repo-relative — the `packages/*` glob. */
function packageDirectories(): string[] {
  return repoFiles(repoRoot)
    .filter((file: string): boolean => /^packages\/[^/]+\/package\.json$/u.test(file))
    .map((file: string): string => dirname(file))
    .toSorted();
}

// A fixture tree, so both detectors are shown to fire on offending content — and
// to stay quiet on content that only records or forbids the package — before they
// are pointed at the repo. Every path here is a fiction inside a tmp dir; the
// real files it imitates are never read by these cases.
const fixtureRoot: string = mkdtempSync(join(tmpdir(), "wallow-web-shell-guard-"));

afterAll(() => {
  rmSync(fixtureRoot, { recursive: true, force: true });
});

function writeFixture(relativePath: string, contents: string | Buffer): void {
  const full: string = join(fixtureRoot, relativePath);
  mkdirSync(dirname(full), { recursive: true });
  writeFileSync(full, contents);
}

/** A lint config's `no-restricted-imports` ban on the package — the earning form. */
const FIXTURE_BAN = `{
  "rules": {
    "no-restricted-imports": [
      "error",
      {
        "paths": [
          {
            "name": ${JSON.stringify(PACKAGE_NAME)},
            "message": ${JSON.stringify(`${PACKAGE_NAME} is deleted. Import createQueryClient from @bc-solutions-coder/query.`)}
          }
        ]
      }
    ]
  }
}
`;

/** The same ban, plus one mention no ban accounts for — dead config. */
const FIXTURE_BAN_WITH_DEAD_GLOB: string = FIXTURE_BAN.replace(
  '{\n  "rules"',
  `{\n  "ignorePatterns": ["packages/${DIRECTORY}/**"],\n  "rules"`,
);

const fixtureFiles: readonly string[] = [
  "docs/development/frontend-setup.md",
  "src/clean.ts",
  "CHANGELOG.md",
  "pnpm-lock.yaml",
  "docs/plans/2026-01-01/0000-note.md",
  "docs/audits/2026-01-01/audit.md",
  "assets/logo.png",
  ".oxlintrc.json",
  "stale-glob/.oxlintrc.json",
  "stale-glob-beside-a-ban/.oxlintrc.json",
  "packages/sdk/src/oxlint-guardrails.test.ts",
  "packages/sdk/src/mentions-only.test.ts",
  "apps/wallow-web/src/router.ts",
  "apps/wallow-web/src/deferred.ts",
  "apps/wallow-web/src/wrapped.ts",
  SELF,
];

writeFixture(
  "docs/development/frontend-setup.md",
  ["| `@bc-solutions-coder/web-shell` | no (`private`) | `.` |", ""].join("\n"),
);
writeFixture("src/clean.ts", 'export const store = "@bc-solutions-coder/query";\n');
writeFixture("CHANGELOG.md", "* **web-shell:** scaffold the package\n");
writeFixture("pnpm-lock.yaml", "  packages/web-shell:\n");
writeFixture("docs/plans/2026-01-01/0000-note.md", "delete packages/web-shell\n");
writeFixture("docs/audits/2026-01-01/audit.md", "web-shell was audited here\n");
// A binary blob: one NUL byte (what marks it binary) and then the token, so
// the NUL sniff is what keeps it out rather than the file's extension.
writeFixture("assets/logo.png", Buffer.concat([Buffer.from([0]), Buffer.from(DIRECTORY, "utf8")]));

// Lint configs: the ban earns the exemption, an unaccounted-for mention does not,
// and a ban does not launder a dead glob sitting beside it.
writeFixture(".oxlintrc.json", FIXTURE_BAN);
writeFixture("stale-glob/.oxlintrc.json", `{ "ignorePatterns": ["packages/${DIRECTORY}/**"] }\n`);
writeFixture("stale-glob-beside-a-ban/.oxlintrc.json", FIXTURE_BAN_WITH_DEAD_GLOB);

// Specs: one that asserts the ban (and therefore carries a violating import as a
// fixture STRING, which is not a resolving reference), and one that merely names
// the package.
writeFixture(
  "packages/sdk/src/oxlint-guardrails.test.ts",
  [
    `const WEB_SHELL = ${JSON.stringify(PACKAGE_NAME)};`,
    `const violating = 'import { createQueryClient } from "${PACKAGE_NAME}";';`,
    'describe("nothing may import the deleted @bc-solutions-coder/web-shell", () => {',
    '  it("fires", () => expect(lint(violating, WEB_SHELL)).not.toHaveLength(0));',
    "});",
    "",
  ].join("\n"),
);
writeFixture(
  "packages/sdk/src/mentions-only.test.ts",
  [
    `const legacy = ${JSON.stringify(PACKAGE_NAME)};`,
    'describe("createQueryClient", () => {',
    '  it("makes a client", () => expect(createQueryClient()).toBeDefined());',
    "});",
    "",
  ].join("\n"),
);

// App modules that really resolve it, in each form a bundler follows.
writeFixture(
  "apps/wallow-web/src/router.ts",
  `import { createQueryClient } from "${PACKAGE_NAME}";\nexport const client = createQueryClient();\n`,
);
writeFixture(
  "apps/wallow-web/src/deferred.ts",
  `export const load = async () => await import(${JSON.stringify(PACKAGE_NAME)});\n` +
    `export const shell = require(${JSON.stringify(PACKAGE_NAME)});\n`,
);
writeFixture(
  "apps/wallow-web/src/wrapped.ts",
  ["import {", "  createQueryClient,", `} from "${PACKAGE_NAME}";`, ""].join("\n"),
);

writeFixture(
  SELF,
  [
    'const PACKAGE_NAME = "@bc-solutions-coder/web-shell";',
    'describe("packages/web-shell is gone from the workspace", () => {',
    '  it("has no directory on disk", () => expect(existsSync(dir)).toBe(false));',
    "});",
    "",
  ].join("\n"),
);

/** The files the stale-reference sweep flags in the fixture tree. */
function flaggedFixtures(): string[] {
  return staleReferences(fixtureRoot, fixtureFiles).map((hit: StaleReference): string => hit.file);
}

describe("the stale-reference sweep detects what it claims to", () => {
  it("flags a live prose reference, and reports the line", () => {
    expect(staleReferences(fixtureRoot, fixtureFiles)).toContainEqual({
      file: "docs/development/frontend-setup.md",
      line: 1,
    });
  });

  it.each([
    "CHANGELOG.md",
    "pnpm-lock.yaml",
    "docs/plans/2026-01-01/0000-note.md",
    "docs/audits/2026-01-01/audit.md",
  ])("leaves the history in %s alone", (file: string) => {
    expect(flaggedFixtures()).not.toContain(file);
  });

  it("does not flag a file that never names the package", () => {
    expect(flaggedFixtures()).not.toContain("src/clean.ts");
  });

  it("skips a binary file whose bytes happen to spell the token", () => {
    expect(flaggedFixtures()).not.toContain("assets/logo.png");
  });

  it("flags a real import, and every other form that resolves the package", () => {
    expect(flaggedFixtures()).toContain("apps/wallow-web/src/router.ts");
    expect(flaggedFixtures()).toContain("apps/wallow-web/src/deferred.ts");
    expect(flaggedFixtures()).toContain("apps/wallow-web/src/wrapped.ts");
  });
});

describe("naming the package is exempt only where forbidding it is the point", () => {
  it("leaves a lint config that names it solely in its own ban alone", () => {
    expect(flaggedFixtures()).not.toContain(".oxlintrc.json");
  });

  it("flags a lint config that names it in a glob instead of a ban, with the line", () => {
    // The dead-config regression: a stale `ignorePatterns` entry pointing at a
    // directory that no longer exists. Nothing in a build fails on it, and the
    // enforcement exemption must not hide it.
    expect(staleReferences(fixtureRoot, fixtureFiles)).toContainEqual({
      file: "stale-glob/.oxlintrc.json",
      line: 1,
    });
  });

  it("flags that same dead glob when a real ban sits beside it", () => {
    // Earned per MENTION, not per file: one accounted-for ban does not buy the
    // config the right to an unaccounted-for second mention.
    expect(flaggedFixtures()).toContain("stale-glob-beside-a-ban/.oxlintrc.json");
  });

  it("leaves a spec that asserts the ban alone, fixture strings included", () => {
    expect(flaggedFixtures()).not.toContain("packages/sdk/src/oxlint-guardrails.test.ts");
  });

  it("flags a spec that names the package without asserting anything about it", () => {
    // The exemption is earned by the enforcement, so deleting the enforcement and
    // leaving the mentions puts them straight back into the sweep.
    expect(flaggedFixtures()).toContain("packages/sdk/src/mentions-only.test.ts");
  });

  it("does not flag this guard, which asserts the package is gone", () => {
    expect(flaggedFixtures()).not.toContain(SELF);
  });
});

describe("the resolving-reference sweep detects what it claims to", () => {
  /** The files the resolving-reference sweep flags in the fixture tree. */
  function resolving(): string[] {
    return resolvingReferences(fixtureRoot, fixtureFiles).map(
      (hit: StaleReference): string => hit.file,
    );
  }

  it.each([
    ["a static import", "apps/wallow-web/src/router.ts", 1],
    ["a multi-line import", "apps/wallow-web/src/wrapped.ts", 3],
    ["a dynamic import", "apps/wallow-web/src/deferred.ts", 1],
  ])("flags %s, and reports the line", (_form: string, file: string, line: number) => {
    expect(resolvingReferences(fixtureRoot, fixtureFiles)).toContainEqual({ file, line });
  });

  it("flags a require() of the package", () => {
    expect(resolvingReferences(fixtureRoot, fixtureFiles)).toContainEqual({
      file: "apps/wallow-web/src/deferred.ts",
      line: 2,
    });
  });

  it("exempts nobody: a ban or a spec may name the package, never import it", () => {
    // Both of these are exempt from the PROSE sweep. Neither is exempt here, and
    // that is the point: the enforcement exemption cannot be used to smuggle in a
    // live import. They are quiet only because they contain no import.
    expect(resolving()).not.toContain(".oxlintrc.json");
    expect(resolving()).not.toContain(SELF);
  });

  it("does not mistake a violating import carried as a fixture string for one", () => {
    // `packages/sdk/src/oxlint-guardrails.test.ts` writes exactly this text into a
    // tmp tree to prove the lint ban fires. A detector that read it as a real
    // import would make writing a lint rule against the package impossible —
    // which is the failure this whole formulation exists to fix.
    expect(resolving()).not.toContain("packages/sdk/src/oxlint-guardrails.test.ts");
  });

  it("does not flag prose that merely names the package", () => {
    expect(resolving()).not.toContain("docs/development/frontend-setup.md");
    expect(resolving()).not.toContain("packages/sdk/src/mentions-only.test.ts");
  });
});

describe("packages/web-shell is gone from the workspace", () => {
  it("has no directory on disk", () => {
    expect(existsSync(resolve(repoRoot, "packages", DIRECTORY))).toBe(false);
  });

  it("has no files under git", () => {
    const tracked: readonly string[] = repoFiles(repoRoot).filter((file: string): boolean =>
      file.startsWith(`packages/${DIRECTORY}/`),
    );

    expect(tracked).toEqual([]);
  });

  it("is no longer a workspace member", () => {
    // `pnpm-workspace.yaml` globs `packages/*`, so membership is the set of
    // manifests on disk rather than a list to edit — derived, not spelled out.
    const names: readonly (string | undefined)[] = packageDirectories().map(
      (dir: string): string | undefined =>
        readManifest(resolve(repoRoot, dir, "package.json")).name,
    );

    expect(names.length, "found no workspace packages to check").toBeGreaterThan(0);
    expect(names).not.toContain(PACKAGE_NAME);
  });

  it.each([...appDirectories(), ...packageDirectories()])(
    "%s declares it in no dependency group",
    (dir: string) => {
      const manifest: Manifest = readManifest(resolve(repoRoot, dir, "package.json"));

      for (const group of DEPENDENCY_GROUPS) {
        expect(
          Object.keys((manifest[group] ?? {}) as Record<string, string>),
          `${dir} still declares ${PACKAGE_NAME} in ${group}`,
        ).not.toContain(PACKAGE_NAME);
      }
    },
  );

  it.each([...appDirectories(), ...packageDirectories()])(
    "%s no longer has it linked into node_modules",
    (dir: string) => {
      // pnpm links a workspace package into an importer only while that importer
      // declares it, so a surviving link is a manifest edit that was never
      // installed — the state in which a source import keeps resolving locally
      // and fails only in a clean checkout.
      expect(existsSync(resolve(repoRoot, dir, "node_modules", PACKAGE_NAME))).toBe(false);
    },
  );

  it("has no importer entry left in the lockfile", () => {
    // Not the lockfile's resolution history (which is allowed to remember the
    // package) — the `importers:` key, whose presence without a matching
    // manifest makes `pnpm install --frozen-lockfile` fail outright. Read as the
    // importer LIST rather than as a substring so a failure reports the members
    // instead of the whole lockfile.
    const importers: readonly string[] = lockfileImporters();

    expect(importers.length, "found no importers in pnpm-lock.yaml").toBeGreaterThan(0);
    expect(importers).not.toContain(`packages/${DIRECTORY}`);
  });
});

describe("nothing in the repo still names the deleted package", () => {
  it("finds files to sweep", () => {
    // Without this the sweep below could go vacuously green: an empty file list
    // yields an empty offence list and fails nothing.
    expect(repoFiles(repoRoot).length).toBeGreaterThan(100);
  });

  it("has no file that imports, requires or otherwise resolves it", () => {
    // The load-bearing lock, and the one with no exemptions: a lint ban is
    // allowed to name the package, and history is allowed to remember it, but
    // nothing is allowed to reach for it.
    expect(resolvingReferences(repoRoot, repoFiles(repoRoot))).toEqual([]);
  });

  it("has no surviving reference outside history and the files that forbid it", () => {
    expect(staleReferences(repoRoot, repoFiles(repoRoot))).toEqual([]);
  });
});

describe("every app Dockerfile copies only packages that exist", () => {
  const dockerfiles: readonly string[] = appDirectories()
    .map((dir: string): string => `${dir}/Dockerfile`)
    .filter((file: string): boolean => existsSync(resolve(repoRoot, file)));

  it("finds the Dockerfiles to check", () => {
    expect(dockerfiles.length).toBeGreaterThan(0);
  });

  it.each(dockerfiles)("%s COPYs no package directory that is absent", (file: string) => {
    const copied: readonly string[] = copiedPackageDirectories(
      readFileSync(resolve(repoRoot, file), "utf8"),
    );

    expect(copied.length, `${file} COPYs no packages/* directory at all`).toBeGreaterThan(0);

    for (const pkg of copied) {
      expect(
        existsSync(resolve(repoRoot, "packages", pkg)),
        `${file} COPYs packages/${pkg}, which does not exist`,
      ).toBe(true);
    }
  });
});

describe("the packages that replaced web-shell are covered by the exports gate", () => {
  const checkExports: string = readFileSync(resolve(repoRoot, "scripts/check-exports.sh"), "utf8");

  it.each(["packages/query", "packages/auth"])("check-exports.sh lists %s", (pkg: string) => {
    // The gate hardcodes its package list, and private packages are included
    // for exports-map hygiene (packages/testing is private and listed). The
    // two packages that absorbed web-shell's surface belong there too.
    const declaration: RegExpMatchArray | null = checkExports.match(/^packages=\((.*)\)$/mu);

    expect(declaration, "check-exports.sh declares no packages=( ... ) array").not.toBeNull();
    expect((declaration?.[1] ?? "").split(/\s+/u)).toContain(pkg);
  });
});
