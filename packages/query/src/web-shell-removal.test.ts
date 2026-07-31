/**
 * `@bc-solutions-coder/web-shell` is deleted, and stays deleted (Wallow-x4qn.11).
 *
 * The package held exactly one symbol, `createQueryClient`, which now lives in this
 * package — so this is the successor asserting its predecessor is gone. The thing
 * being shipped is an ABSENCE, and the parts of an absence that a build does not
 * notice are what need a lock.
 *
 * WHO ENFORCES WHAT, since Wallow-l5x2 cut this file from 766 lines to this. The
 * load-bearing lock — that nothing RESOLVES the package — is lint's: the repo-root
 * `.oxlintrc.json` carries a `no-restricted-imports` ban on the name, every nested
 * config restates it, and `packages/sdk/src/oxlint-guardrails.test.ts` pins that it
 * is there. A rule reports the offending import at the import; the ~600 lines
 * deleted from here reimplemented it as a `git ls-files` sweep with regexes for
 * each resolving form, an earned-exemption model so that a lint config and a spec
 * were allowed to NAME what they forbid, and a whole tmp fixture tree to prove
 * those detectors fired. All of that machinery existed to let a text scan
 * distinguish an import from a mention — which is the one thing a linter does not
 * have to be taught.
 *
 * Gone with it is the PROSE sweep, deliberately. A stale row in a docs table is a
 * broken instruction for a fork, but it is also the kind of drift a reader fixes in
 * seconds, and it was not worth a scanner that had to model which files are allowed
 * to say the word.
 *
 * WHAT SURVIVES IS WHAT NO RULE AND NO COMPILER CAN SEE — the workspace-level
 * residue of a deleted package, all of it DERIVED from disk rather than listed:
 *
 *  1. The directory is gone, no workspace member is named after it, no manifest
 *     declares it, no importer has it linked, and the lockfile has no `importers:`
 *     entry for it (an importer entry without a matching manifest makes
 *     `pnpm install --frozen-lockfile` — the first command in both app Dockerfiles
 *     — an error, minutes into a CI image build and never in `pnpm check`).
 *  2. No app Dockerfile COPYs a `packages/<name>` directory that does not exist.
 *     The per-app `docker-workspace-copies.test.ts` specs assert the forward
 *     direction (every declared workspace dep is copied); this is the reverse,
 *     which is the direction a DELETION breaks.
 *  3. `scripts/check-exports.sh` lists the packages that replaced it, so the
 *     exports-map gate covers the successors rather than silently shrinking.
 *
 * Node project: reads files, mounts nothing.
 */
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

// packages/query/src -> repo root (src -> query -> packages -> repo).
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

/** The deleted package, by directory name and by package name. */
const DIRECTORY = "web-shell";
const PACKAGE_NAME = "@bc-solutions-coder/web-shell";

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

/**
 * Every file git would show over a clean checkout: tracked files plus untracked
 * ones that are not ignored. Using git's own view keeps `node_modules/`, `dist/`
 * and `.output/` out without a hand-maintained skip list.
 */
function repoFiles(): string[] {
  return execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard"], {
    cwd: repoRoot,
    encoding: "utf8",
    maxBuffer: 32 * 1024 * 1024,
  })
    .split("\n")
    .filter((file: string): boolean => file.length > 0);
}

/**
 * The workspace members `pnpm-lock.yaml`'s `importers:` block declares. Each is a
 * two-space-indented key holding the path of a directory that must still carry a
 * `package.json`; the block ends at the next top-level key.
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
  return repoFiles()
    .filter((file: string): boolean =>
      /^apps\/(?:[^/]+|examples\/[^/]+)\/package\.json$/u.test(file),
    )
    .map((file: string): string => dirname(file))
    .toSorted();
}

/** Every workspace package's directory, repo-relative — the `packages/*` glob. */
function packageDirectories(): string[] {
  return repoFiles()
    .filter((file: string): boolean => /^packages\/[^/]+\/package\.json$/u.test(file))
    .map((file: string): string => dirname(file))
    .toSorted();
}

describe("packages/web-shell is gone from the workspace", () => {
  it("has no directory on disk", () => {
    expect(existsSync(resolve(repoRoot, "packages", DIRECTORY))).toBe(false);
  });

  it("has no files under git", () => {
    const tracked: readonly string[] = repoFiles().filter((file: string): boolean =>
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
      // installed — the state in which a source import keeps resolving locally and
      // fails only in a clean checkout.
      expect(existsSync(resolve(repoRoot, dir, "node_modules", PACKAGE_NAME))).toBe(false);
    },
  );

  it("has no importer entry left in the lockfile", () => {
    // Not the lockfile's resolution history (which is allowed to remember the
    // package) — the `importers:` key, whose presence without a matching manifest
    // makes `pnpm install --frozen-lockfile` fail outright. Read as the importer
    // LIST rather than as a substring so a failure reports the members instead of
    // the whole lockfile.
    const importers: readonly string[] = lockfileImporters();

    expect(importers.length, "found no importers in pnpm-lock.yaml").toBeGreaterThan(0);
    expect(importers).not.toContain(`packages/${DIRECTORY}`);
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
    // The gate hardcodes its package list, and private packages are included for
    // exports-map hygiene (packages/testing is private and listed). The two
    // packages that absorbed web-shell's surface belong there too.
    const declaration: RegExpMatchArray | null = checkExports.match(/^packages=\((.*)\)$/mu);

    expect(declaration, "check-exports.sh declares no packages=( ... ) array").not.toBeNull();
    expect((declaration?.[1] ?? "").split(/\s+/u)).toContain(pkg);
  });
});
