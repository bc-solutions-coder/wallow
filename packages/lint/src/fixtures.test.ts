/**
 * The generic fixture runner for Wallow's own oxlint rules.
 *
 * One spec drives every rule. `fixtures/<rule>/` holds `valid.tsx` (must report nothing) and
 * `invalid.tsx` (every expected diagnostic marked by an `// expect-error: wallow/<rule>` comment
 * on the line before it). The real oxlint binary runs once per fixture directory, and the
 * reported (file, line, rule) multiset must equal the annotated one EXACTLY — an unannotated
 * diagnostic and an annotation nothing fired on both fail.
 *
 * Measured facts this depends on (oxlint 1.74.0):
 *   - `--format=json` prints ONE JSON object on stdout, but prefixes it with the bare line
 *     `No files found to lint.` when nothing matched, which makes `JSON.parse(stdout)` throw.
 *     Parsing from the first `{` and then asserting `number_of_files` is the reliable read.
 *   - A diagnostic's rule id is `code`, spelled `wallow(no-tinted-text)`; its line is
 *     `labels[0].span.line`, 1-based. A SYNTAX error carries no `code` at all and suppresses
 *     every lint diagnostic in the file, so a missing `code` has to fail loudly.
 *   - `-c` replaces the root config outright, but oxlint's DEFAULT `correctness` category is
 *     still on unless the config turns it off, which the base fixture config does.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

const PACKAGE_DIR = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const REPO_ROOT = resolve(PACKAGE_DIR, "..", "..");
const FIXTURES_DIR = join(PACKAGE_DIR, "fixtures");
const BASE_CONFIG = join(FIXTURES_DIR, "fixture.oxlintrc.json");
const OXLINT = join(REPO_ROOT, "node_modules", ".bin", "oxlint");

const ANNOTATION = /^\s*(?:\{\s*)?(?:\/\/|\/\*)\s*expect-error:\s*(\S+)/u;
const RULE_ID = /^(?<plugin>[\w-]+)\((?<rule>[\w/-]+)\)$/u;

interface Diagnostic {
  readonly file: string;
  readonly line: number;
  readonly rule: string;
}

/** `wallow(no-tinted-text)` — oxlint's spelling — as `wallow/no-tinted-text`, as fixtures write. */
function normalizeRuleId(code: string): string {
  const match = code.match(RULE_ID);
  return match === null ? code : `${match.groups!.plugin}/${match.groups!.rule}`;
}

/**
 * A config enabling exactly one rule, with an absolute plugin path so it resolves from the
 * temp directory it is written into. `fixtures/<rule>/options.json`, when present, supplies
 * the rule's options.
 */
function configFor(rule: string): string {
  const base = JSON.parse(readFileSync(BASE_CONFIG, "utf8")) as Record<string, unknown>;
  const optionsPath = join(FIXTURES_DIR, rule, "options.json");
  const severity: unknown = existsSync(optionsPath)
    ? ["error", JSON.parse(readFileSync(optionsPath, "utf8"))]
    : "error";
  const temporaryDirectory = mkdtempSync(join(tmpdir(), "wallow-lint-"));
  const path = join(temporaryDirectory, "config.json");

  writeFileSync(
    path,
    JSON.stringify({
      ...base,
      jsPlugins: [join(PACKAGE_DIR, "src", "index.ts")],
      rules: { [`wallow/${rule}`]: severity },
    }),
  );

  return path;
}

function fixtureFiles(directory: string): readonly string[] {
  return readdirSync(directory, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && /\.tsx?$/u.test(entry.name))
    .map((entry) => join(entry.parentPath, entry.name))
    .toSorted();
}

/**
 * Every diagnostic oxlint reports for `directory`, and the file count it actually linted.
 *
 * The binary exits 1 on any error, so a non-zero status is expected and its stdout is the
 * payload. What is NOT tolerated is stdout carrying no JSON object at all.
 */
function runOxlint(
  rule: string,
  directory: string,
): { diagnostics: readonly Diagnostic[]; fileCount: number } {
  const config = configFor(rule);
  let stdout: string;

  try {
    stdout = execFileSync(OXLINT, ["-c", config, "--format=json", directory], {
      cwd: REPO_ROOT,
      encoding: "utf8",
    });
  } catch (error) {
    const failure = error as { stdout?: string; stderr?: string };

    if (typeof failure.stdout !== "string" || !failure.stdout.includes("{")) {
      throw new Error(
        `oxlint produced no JSON for ${directory}.\nstdout: ${failure.stdout}\nstderr: ${failure.stderr}`,
        { cause: error },
      );
    }

    stdout = failure.stdout;
  }

  const payload = JSON.parse(stdout.slice(stdout.indexOf("{"))) as {
    diagnostics: {
      code?: string;
      message: string;
      filename: string;
      labels: { span: { line: number } }[];
    }[];
    number_of_files: number;
  };

  const diagnostics = payload.diagnostics.map((entry): Diagnostic => {
    if (entry.code === undefined) {
      throw new Error(
        `${entry.filename}: oxlint reported a diagnostic with no rule id, which means the ` +
          `fixture does not parse. Lint diagnostics are suppressed for that file. ` +
          `Message: ${entry.message}`,
      );
    }

    return {
      file: relative(PACKAGE_DIR, resolve(REPO_ROOT, entry.filename)),
      line: entry.labels[0]!.span.line,
      rule: normalizeRuleId(entry.code),
    };
  });

  return { diagnostics, fileCount: payload.number_of_files };
}

/**
 * The `// expect-error: <rule>` markers in `file`.
 *
 * An annotation applies to the next line that is not itself an annotation, so two stacked
 * markers both name the same target line — which is how a fixture states that one line raises
 * two diagnostics.
 */
function annotationsIn(file: string): readonly Diagnostic[] {
  const lines = readFileSync(file, "utf8").split("\n");
  const found: Diagnostic[] = [];

  for (const [index, text] of lines.entries()) {
    const match = text.match(ANNOTATION);

    if (match !== null) {
      let target = index + 1;

      while (target < lines.length && ANNOTATION.test(lines[target]!)) {
        target += 1;
      }

      found.push({ file: relative(PACKAGE_DIR, file), line: target + 1, rule: match[1]! });
    }
  }

  return found;
}

function serialize(entries: readonly Diagnostic[]): readonly string[] {
  return entries.map((entry) => `${entry.file}:${entry.line} ${entry.rule}`).toSorted();
}

const ruleDirectories = readdirSync(FIXTURES_DIR, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .toSorted();

describe("wallow oxlint rule fixtures", () => {
  it("discovers at least one fixture directory", () => {
    expect(ruleDirectories.length).toBeGreaterThan(0);
  });

  describe.each(ruleDirectories)("%s", (rule) => {
    const directory = join(FIXTURES_DIR, rule);

    it("reports exactly the annotated diagnostics and nothing else", () => {
      const files = fixtureFiles(directory);
      const { diagnostics, fileCount } = runOxlint(rule, directory);

      // The loud-failure guard: oxlint prints an empty diagnostic list when it matches no
      // files, which would otherwise read as "the valid fixture is clean".
      expect(fileCount, `oxlint linted ${fileCount} files under ${directory}`).toBe(files.length);

      const annotated = files.flatMap((file) => annotationsIn(file));

      expect(annotated.length, `${rule} annotates no diagnostics`).toBeGreaterThan(0);
      expect(serialize(diagnostics)).toStrictEqual(serialize(annotated));
    });
  });
});
