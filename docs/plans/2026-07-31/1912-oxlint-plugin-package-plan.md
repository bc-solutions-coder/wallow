# `@bc-solutions-coder/lint` Implementation Plan — zone-dag scope

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**status: active**

**Design doc:** `docs/plans/2026-07-31/1843-oxlint-plugin-package.md` — read it first. This plan
implements a **deliberately reduced slice** of it and does not restate its rationale.

**Goal:** Stand up `packages/lint`, move the four existing rules into it, delete `tools/`, and
retire both apps' `zone-dag.test.ts` (748 lines) onto a single lint rule.

**Scope decision.** The design doc classifies four guard specs as convertible. This plan converts
**one** — `zone-dag`, the largest at 748 lines across two apps. `server-only-naming`,
`feature-barrels` and `client-navigation` are analysed in the design doc and stay on disk; file
beads for them if they are wanted later. The rule generator, the generated registry, the
suppression census and the `wallow-lint-rule` skill are also **deferred**: they earn their keep
across many rules, and this plan writes one. The fixture harness is *not* deferred — it is what
makes a rule replacing 748 lines of assertions safe to write.

**Architecture:** `packages/lint` exports an oxlint JS plugin built with `@oxlint/plugins`
(`definePlugin`/`defineRule`/`createOnce`). In-repo it resolves as TypeScript source (Node 24
strips types); on publish, `publishConfig` swaps `exports` to `dist/`. Rules are registered from
the two **nested** app configs only — never the repo root.

**Tech Stack:** oxlint 1.74.0, `@oxlint/plugins` 1.76.0, TypeScript 7.0.2, vitest (node project),
pnpm workspace, Node 24.

---

## Before you start

**Work in a fresh worktree off `main`.** A spike worktree may exist at
`/Users/traveler/Repos/Wallow-lint-spike` — it proved feasibility only and its contents are
throwaway. Do not cherry-pick from it.

```bash
cd /Users/traveler/Repos/Wallow
git worktree add ../Wallow-lint ./ -b feat/oxlint-plugin-package
cd ../Wallow-lint && pnpm install
```

**Track work in beads** (`bd`), not TodoWrite. **Never `git add` anything under `docs/plans/`** —
it is gitignored. Conventional commits, lowercase imperative, first line < 72 chars.

**Verified facts — probed against the real binary, do not re-derive:**

| Fact | Value |
| --- | --- |
| `context.filename` | absolute path |
| `createOnce` setup | runs **once per process**, can do `node:fs` I/O |
| Bare specifier in nested `jsPlugins` | resolves |
| `.ts` plugin entry | loads on Node 24 |
| Relative imports inside the plugin | **must** carry `.ts` — extensionless throws `ERR_MODULE_NOT_FOUND` |
| `--format=json` | prefixes a bare `No files found to lint.` line when nothing matched |
| Diagnostic shape | rule id in `code` as `wallow(rule-name)`; line at `labels[0].span.line` (1-based) |
| Syntax error | carries **no** `code` and suppresses all lint diagnostics in that file |
| `-c` | replaces the root config outright; default `correctness` category stays on unless disabled |

---

## Task 1: Scaffold `packages/lint`

**Files:**
- Create: `packages/lint/package.json`
- Create: `packages/lint/tsconfig.json`
- Create: `packages/lint/vitest.config.ts`

**Step 1: Write `packages/lint/package.json`**

```json
{
  "name": "@bc-solutions-coder/lint",
  "version": "0.0.0",
  "private": true,
  "description": "Wallow's own oxlint JS plugin rules",
  "type": "module",
  "main": "./dist/index.js",
  "module": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": { "types": "./src/index.ts", "import": "./src/index.ts" }
  },
  "publishConfig": {
    "exports": {
      ".": { "types": "./dist/index.d.ts", "import": "./dist/index.js" }
    }
  },
  "files": ["dist"],
  "scripts": {
    "typecheck": "tsc --noEmit",
    "test": "vitest run"
  },
  "dependencies": {
    "@oxlint/plugins": "^1.76.0"
  },
  "devDependencies": {
    "@types/node": "^24.0.0",
    "typescript": "catalog:tooling",
    "vitest": "catalog:tooling"
  }
}
```

> `@oxlint/plugins` is a **runtime dependency**, not a devDependency — a consumer installing the
> published package needs it at plugin load time.

No `build` script yet: nothing publishes this today, and `publishConfig` is wired so it *can* be
built later. Check the `tooling` catalog in `pnpm-workspace.yaml` actually carries `vitest` and
`typescript` entries; if not, copy the literal versions another package uses rather than inventing
them.

**Step 2: Write `packages/lint/tsconfig.json`**

```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "allowImportingTsExtensions": true,
    "noEmit": true,
    "types": ["node"]
  },
  "include": ["src"]
}
```

`include` is `src` only — fixtures must NOT be typechecked.

**Step 3: Write `packages/lint/vitest.config.ts`** — node project only, no browser:

```ts
import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["src/**/*.test.ts"],
  },
});
```

**Step 4: Install and verify the workspace picks it up**

```bash
pnpm install
pnpm --filter @bc-solutions-coder/lint exec node -e "console.log('ok')"
```
Expected: `ok`. If pnpm cannot find the filter, `packages/*` in `pnpm-workspace.yaml` is not
matching — stop and fix that first.

**Step 5: Commit**

```bash
git add packages/lint pnpm-lock.yaml
git commit -m "build(lint): scaffold the packages/lint workspace package"
```

---

## Task 2: Move the four existing rules, verbatim

This is a **move, not a rewrite.** The rule bodies and every word of their doc comments are already
correct and already reviewed. Changing behaviour here would make a regression impossible to spot.

**Files:**
- Create: `packages/lint/src/rules/no-sidebar-inversion.ts`
- Create: `packages/lint/src/rules/no-tinted-text.ts`
- Create: `packages/lint/src/rules/text-heading-variant.ts`
- Create: `packages/lint/src/rules/no-hand-rolled-mutation.ts`
- Create: `packages/lint/src/index.ts`
- Source: `tools/oxlint/wallow-lint-plugin.js` (read-only this task; deleted in Task 7)

**Step 1: Split the file, one rule per module**

For each rule, copy its const and every helper it uses, **with all doc comments intact**:

| New file | Copy from source | Helpers it needs |
| --- | --- | --- |
| `no-sidebar-inversion.ts` | lines 59–148 | `INVERSION_UTILITIES`, `offenders()` |
| `text-heading-variant.ts` | lines 150–288 | `HEADING_LEVELS`, `attributesByName()`, `stringValue()` |
| `no-tinted-text.ts` | lines 290–376 | `TINTABLE_TOKENS`, `TINTED_TEXT` |
| `no-hand-rolled-mutation.ts` | lines 378–422 | none |

Changes permitted in this task, and **only** these:

1. Wrap each rule object in `defineRule({ ... })` from `@oxlint/plugins`.
2. Change `create(context)` → `createOnce(context)`. All four are stateless per file, so this is
   safe — verify that per rule as you go; a rule accumulating per-file state would need a
   `before()` hook to reset it, and none of these do.
3. Export named: `export const noSidebarInversion = defineRule({...})`.
4. Add types where TypeScript demands them. Do not restructure logic to make types easier.

The file header (source lines 1–57) is **registration and constraint documentation**, not rule
documentation. It belongs to no single rule and moves to `packages/lint/CLAUDE.md` in Task 3.

**Step 2: Write `packages/lint/src/index.ts`**

```ts
import { definePlugin, eslintCompatPlugin } from "@oxlint/plugins";

import { noHandRolledMutation } from "./rules/no-hand-rolled-mutation.ts";
import { noSidebarInversion } from "./rules/no-sidebar-inversion.ts";
import { noTintedText } from "./rules/no-tinted-text.ts";
import { textHeadingVariant } from "./rules/text-heading-variant.ts";

export default eslintCompatPlugin(
  definePlugin({
    meta: { name: "wallow" },
    rules: {
      "no-hand-rolled-mutation": noHandRolledMutation,
      "no-sidebar-inversion": noSidebarInversion,
      "no-tinted-text": noTintedText,
      "text-heading-variant": textHeadingVariant,
    },
  }),
);
```

**The `.ts` extensions are mandatory.** oxlint loads this as plain Node ESM, which rejects
extensionless relative specifiers with `ERR_MODULE_NOT_FOUND` — and TypeScript will not warn you,
because it typechecks clean either way. Same trap `packages/config/CLAUDE.md` documents.

**Step 3: Typecheck**

```bash
pnpm --filter @bc-solutions-coder/lint typecheck
```
Expected: clean.

**Step 4: Prove the plugin loads before wiring any app to it**

```bash
cd packages/lint
cat > /tmp/probe.json <<'EOF'
{
  "jsPlugins": ["./src/index.ts"],
  "plugins": [],
  "categories": { "correctness": "off", "suspicious": "off", "perf": "off",
                  "pedantic": "off", "style": "off", "restriction": "off", "nursery": "off" },
  "rules": { "wallow/no-tinted-text": "error" }
}
EOF
printf 'export const a = "text-foreground/60";\n' > /tmp/probe.tsx
../../node_modules/.bin/oxlint -c /tmp/probe.json /tmp/probe.tsx
```
Expected: one `error wallow(no-tinted-text)` diagnostic. `Cannot find module` means you dropped a
`.ts` extension in `index.ts`.

**Step 5: Commit**

```bash
git add packages/lint/src
git commit -m "refactor(lint): move the four custom rules into packages/lint"
```

---

## Task 3: Write `packages/lint/CLAUDE.md`

**Files:**
- Create: `packages/lint/CLAUDE.md`

This file is the reason the next person does not re-break the two constraints that cost the most to
discover. It must carry, in full:

- **Never register from the repo-root `.oxlintrc.json`.** Reproduce source lines 16–34 verbatim —
  `packages/sdk/src/oxlint-guardrails.test.ts` copies the root config to a temp dir; any
  `jsPlugins` entry makes the copy unloadable; the spec's `JSON.parse` throws and **0 tests run**.
  Include the measured finding that no specifier form rescues it, relative or bare.
- **Nested configs must restate** the root's `apps/<app>/**` override block and `ignorePatterns`
  (source lines 41–47) — oxlint matches both relative to the config's own directory.
- **Both apps are gated, not identically** (source lines 49–56): wallow-auth adds `button` to the
  forbid list and owns `text-heading-variant`; wallow-web's `bff-demo` deliberately ships four raw
  `<button>`s and takes `Text`'s derived scale.
- **Relative imports inside `src/` need `.ts` extensions**, and why.
- The rule-vs-test boundary in one sentence: **a rule sees one JS/TS file at a time, and only files
  oxlint lints.** Plus the corollary that `ignorePatterns` makes a rule silent where a disk sweep is
  loud.
- A pointer to the design doc's Section B for the three specs analysed but not converted.

**Commit**

```bash
git add packages/lint/CLAUDE.md
git commit -m "docs(lint): document the registration constraints in packages/lint"
```

---

## Task 4: Point the two app configs at the package

**Files:**
- Modify: `apps/wallow-web/.oxlintrc.json:4`
- Modify: `apps/wallow-auth/.oxlintrc.json` (its `jsPlugins` line)

**Step 1: Add the workspace dependency to both apps**

```bash
pnpm --filter @bc-solutions-coder/wallow-web add -D @bc-solutions-coder/lint@workspace:*
pnpm --filter @bc-solutions-coder/wallow-auth add -D @bc-solutions-coder/lint@workspace:*
```

**Step 2: Swap the specifier in both configs**

Replace `"jsPlugins": ["../../tools/oxlint/wallow-lint-plugin.js"],` with:

```json
"jsPlugins": [{ "name": "wallow", "specifier": "@bc-solutions-coder/lint" }],
```

Change **nothing else** in either file. Every `rules` and `overrides` entry stays exactly as it is —
the two apps' rule sets differ deliberately and this task must not converge them.

**Step 3: Verify both apps lint identically to before**

```bash
git stash && pnpm lint > /tmp/lint-before.txt 2>&1; git stash pop
pnpm lint > /tmp/lint-after.txt 2>&1
diff /tmp/lint-before.txt /tmp/lint-after.txt
```
Expected: **no diff.** A move that changes what fires is a bug, not a refactor. Non-empty diff
means you changed a rule body in Task 2 — go back.

**Step 4: Verify the guardrail spec still collects**

This is the spec that silently reports 0 tests when the root config breaks:

```bash
pnpm --filter @bc-solutions-coder/sdk test oxlint-guardrails
```
Expected: a **non-zero** test count, all passing. "0 tests" is a failure even at exit code 0.

**Step 5: Commit**

```bash
git add apps/wallow-web/.oxlintrc.json apps/wallow-auth/.oxlintrc.json \
        apps/wallow-web/package.json apps/wallow-auth/package.json pnpm-lock.yaml
git commit -m "build(lint): load the wallow plugin from the workspace package"
```

---

## Task 5: Ignore fixtures repo-wide, before writing any

Fixtures contain **deliberate violations**. `pnpm lint` lints `packages`, and `oxfmt` would
reformat fixture files and move `expect-error` annotations off their target lines. Both must be
excluded first, or every later task fails the gate for the wrong reason.

**Files:**
- Modify: `.oxlintrc.json` (`ignorePatterns`)
- Modify: `.oxfmtrc.json` (`ignorePatterns`)

**Step 1:** Add `"packages/lint/fixtures/**"` to the `ignorePatterns` array in **both** files.

**Step 2: Verify**

```bash
mkdir -p packages/lint/fixtures/probe
printf 'export const a = "text-foreground/60";\nlet x=1\n' > packages/lint/fixtures/probe/invalid.tsx
pnpm lint && pnpm format:check
rm -rf packages/lint/fixtures/probe
```
Expected: both pass.

**Step 3: Commit**

```bash
git add .oxlintrc.json .oxfmtrc.json
git commit -m "build(lint): exclude rule fixtures from lint and format"
```

---

## Task 6: The fixture harness

**Files:**
- Create: `packages/lint/src/fixtures.test.ts`
- Create: `packages/lint/fixtures/fixture.oxlintrc.json`
- Create: `packages/lint/fixtures/no-tinted-text/{valid,invalid}.tsx`

**Step 1: Write `packages/lint/fixtures/fixture.oxlintrc.json`**

```json
{
  "jsPlugins": [],
  "plugins": [],
  "categories": {
    "correctness": "off", "suspicious": "off", "perf": "off",
    "pedantic": "off", "style": "off", "restriction": "off", "nursery": "off"
  },
  "rules": {}
}
```

Every category is off because `-c` replaces the root config but leaves oxlint's default
`correctness` category **on** — without this, unrelated built-in diagnostics pollute every fixture.
`jsPlugins` and `rules` are filled in per-run by the harness.

**Step 2: Write `packages/lint/src/fixtures.test.ts`**

The harness discovers `fixtures/<rule>/`, writes a temp config enabling exactly `wallow/<rule>`,
runs the real binary against that directory, and asserts the reported `(file, line, rule)` multiset
equals the annotated one exactly. Use this implementation — its details are measured, not stylistic:

```ts
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
  const path = join(mkdtempSync(join(tmpdir(), "wallow-lint-")), "config.json");

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
    if (match === null) continue;

    let target = index + 1;
    while (target < lines.length && ANNOTATION.test(lines[target]!)) target += 1;

    found.push({ file: relative(PACKAGE_DIR, file), line: target + 1, rule: match[1]! });
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
```

Note this reads annotations from **every** fixture file, not just `invalid.tsx` — Task 8 needs a
nested directory tree, and a `valid.tsx` simply carries no annotations.

**Step 3: Add the first fixture**

`packages/lint/fixtures/no-tinted-text/valid.tsx`:
```tsx
export function Valid() {
  return <div className="text-muted-foreground bg-foreground/40">muted copy on a scrim</div>;
}
```

`packages/lint/fixtures/no-tinted-text/invalid.tsx`:
```tsx
export function Invalid() {
  return (
    <div>
      {/* expect-error: wallow/no-tinted-text */}
      <p className="text-foreground/60">muted</p>
      {/* expect-error: wallow/no-tinted-text */}
      <span className="hover:text-primary/80">hover</span>
    </div>
  );
}
```

`valid.tsx` deliberately includes `bg-foreground/40` — the drawer-scrim case the rule must NOT
report. A valid fixture that only omits violations proves nothing about the rule's boundary.

**Step 4: Run**

```bash
pnpm --filter @bc-solutions-coder/lint test
```
Expected: PASS.

**Step 5: Prove the harness fails correctly**

Append an unannotated `text-primary/70` to `valid.tsx`, re-run, confirm it fails naming the exact
`file:line rule`, then revert. A harness never seen red is a harness you do not know works.

**Step 6: Commit**

```bash
git add packages/lint/src/fixtures.test.ts packages/lint/fixtures
git commit -m "test(lint): add the fixture harness and the first rule fixture"
```

---

## Task 7: Delete `tools/`

`tools/` contains **only** `oxlint/wallow-lint-plugin.js`. Nothing loads it after Task 4, so it
goes — and so do its references in two root scripts, which would otherwise point at a path that no
longer exists.

**Files:**
- Delete: `tools/oxlint/wallow-lint-plugin.js` (and the now-empty `tools/`)
- Modify: root `package.json` — remove `tools` from both the `lint` and `format` script path lists

**Step 1: Confirm nothing references it**

```bash
rg -n "tools/oxlint|wallow-lint-plugin" --hidden -g '!node_modules'
```
Expected: no hits outside `docs/plans/`.

**Step 2:** Delete it, and drop `tools` from `lint` and `format` in root `package.json`.

**Step 3: Verify**

```bash
pnpm lint && pnpm format:check
```

An oxlint invocation naming a nonexistent path can lint **zero files and exit 0**, so confirm the
file count did not collapse rather than trusting the exit code:

```bash
./node_modules/.bin/oxlint apps packages \
  --ignore-pattern '**/*.test.*' --ignore-pattern '**/*.stories.tsx' 2>&1 | tail -3
```
Expected: a file count comparable to the pre-change run.

**Step 4: Commit**

```bash
git add -A tools package.json
git commit -m "refactor: delete tools/ now that the lint plugin is a package"
```

---

## Task 8: `wallow/zone-dag`

Retires `apps/wallow-web/src/zone-dag.test.ts` and its byte-identical wallow-auth twin — 748 lines
across the two apps — onto one rule.

**Files:**
- Create: `packages/lint/src/rules/zone-dag.ts`
- Create: `packages/lint/fixtures/zone-dag/**`
- Modify: `packages/lint/src/index.ts`
- Modify: both apps' `.oxlintrc.json`
- **Delete: `apps/wallow-web/src/zone-dag.test.ts`, `apps/wallow-auth/src/zone-dag.test.ts`** (748 lines)

**Both files delete outright.** Each holds three blocks and none survives:

| Block | Lines | Fate |
| --- | --- | --- |
| `describe("the zone walk itself")` | 263–280 | **Deleted** — it guards the sweep, and the sweep is going. Its fail-closed role passes to the harness's `number_of_files` assertion. |
| `describe("the import DAG")` | 282–355 | **Retires onto the rule** — all six assertions. |
| `describe("shared/")` | 357–374 | **Deleted, as a policy change.** It asserted `shared/` contains only `components`, `hooks`, `lib`, `stores`, `testing`, `types`. **`shared/` is no longer shape-locked to a fixed subdirectory list.** Drop the assertion and the `SHARED_SUBDIRS` constant; do not port either to the rule. |

Do not confuse that last row with the DAG edge **"`shared/` may not import a feature"** — that is
about the *direction* of a dependency, not the shape of a directory, and it stays as one of the
rule's six checks.

**Step 1: Write the fixture tree first**

The rule classifies files by their path, so fixtures need a directory layout it can read. Build a
miniature app under `packages/lint/fixtures/zone-dag/`:

```
fixtures/zone-dag/
  tsconfig.json                        # declares the three zone paths
  src/
    app/routes/dashboard.tsx
    features/login/index.ts
    features/login/valid.tsx
    features/login/invalid.tsx
    features/signup/index.ts
    shared/lib/thing.ts
    shared/invalid.tsx
```

`fixtures/zone-dag/tsconfig.json` needs only:

```json
{
  "compilerOptions": {
    "paths": {
      "@app/*": ["./src/app/*"],
      "@features/*": ["./src/features/*"],
      "@shared/*": ["./src/shared/*"]
    }
  }
}
```

`src/features/login/invalid.tsx` — one case per retired assertion:

```tsx
// expect-error: wallow/zone-dag
import { thing } from "../../shared/lib/thing";
// expect-error: wallow/zone-dag
import { deep } from "@features/signup/nested/thing";
// expect-error: wallow/zone-dag
import { sibling } from "@features/signup";
// expect-error: wallow/zone-dag
import { route } from "@app/routes/dashboard";
// expect-error: wallow/zone-dag
import { escaped } from "../../../../elsewhere";

export const all = [thing, deep, sibling, route, escaped];
```

`src/shared/invalid.tsx` — the assertion that has no exemption at all:

```tsx
// expect-error: wallow/zone-dag
import { login } from "@features/login";

export const x = login;
```

`src/features/login/valid.tsx` must exercise the boundary, not just avoid it: a relative import
*within* the zone (`./index.ts`), and an aliased `@shared/lib/thing` — both legal and both easy to
break.

**Step 2: Run the harness, watch it fail** (rule does not exist yet).

**Step 3: Implement**

Port `zoneOf()` (spec lines 194–204) and `targetOf()` (lines 207–238) essentially verbatim — they
are already correct TypeScript. Replace the disk sweep with visitors.

*App-root resolution.* Walk up from `context.filename` to the nearest directory containing a
`tsconfig.json` with `compilerOptions.paths`; cache per directory in `createOnce`. This is what
makes the rule work in both apps **with zero options** — do not add an option for the src path.
Read the `paths` map with the comment-stripping from spec lines 52–63: `tsconfig.json` carries `//`
comments and `JSON.parse` rejects them.

*Visitors.* `ImportDeclaration`, `ExportNamedDeclaration`, `ExportAllDeclaration`, and
`ImportExpression` (dynamic `import("…")`). The spec's header calls dynamic import "exactly how a
module reaches something it may not reach at module scope" — a rule blind to it has a hole shaped
like the violation it exists to catch. Only string-literal specifiers are judged, matching the spec.

*Carry these across, each load-bearing:*

- `BARREL_ZONES` — an option defaulting to `["features"]`. `@features/login` is the contract;
  `@features/login/anything` reaches around it. Every other zone is a flat namespace.
- **`SPEC_MAY_REACH_APP`** — a spec may import `@app/*`, because mounting the real route is how a
  screen's contract is tested. Express this as a **rule option**, not a config override: an
  override turning the rule off for `**/*.test.tsx` would also exempt specs from the other five
  checks, which is a widening the spec never granted. Keep it as a per-messageId exemption inside
  the rule.
- Zone `"root"` — a policy spec directly under `src/` — is outside the product graph and reports
  nothing.
- `routeTree.gen.ts` is already in both apps' `ignorePatterns`, so the rule never sees it. **Confirm
  that rather than assuming**; it is the difference between "exempt" and "silently unchecked".

One `messageId` per violation kind — `relativeCrossZone`, `deepIntoFeature`, `siblingFeature`,
`reachesBackIntoApp`, `sharedReachesFeature`, `escapesSrc` — each carrying the guidance the
corresponding spec assertion's comment carries today. Those comments are the rule's documentation;
do not paraphrase them away.

**Step 4:** Harness green.

**Step 5: Enable in both apps**

Add `"wallow/zone-dag": "error"` to both `.oxlintrc.json` files, then:

```bash
pnpm lint
```
Expected: **zero violations.** Both specs are green on `main`, so anything reported is either a rule
bug or something the spec's regex missed. **Investigate before suppressing** — the second case is a
real bug and is the entire argument for this migration. If you find one, fix the app and say so in
the commit body.

**Step 6: Delete both specs**

```bash
git rm apps/wallow-web/src/zone-dag.test.ts apps/wallow-auth/src/zone-dag.test.ts
pnpm test
```

Nothing is carried over. If you find yourself wanting to keep a helper, it belongs in the rule.

**Step 7: Commit**

```bash
git add packages/lint apps/wallow-web apps/wallow-auth
git commit -m "refactor(lint): replace the zone-DAG disk sweeps with a lint rule"
```

---

## Task 9: Documentation, full gate, close out

**Files:**
- Modify: `CLAUDE.md`
- Modify: `apps/CLAUDE.md`
- Modify: `docs/development/frontend-setup.md:118`

**Step 1: Update the docs to match reality**

- `CLAUDE.md` — add `packages/lint/` to the repo layout table; update the linting paragraph to say
  the custom rules live in the package.
- `apps/CLAUDE.md` — three passages are now wrong, all in the `## apps` bullet list:
  - Lines 60–79 name `tools/oxlint/wallow-lint-plugin.js` and "three custom rules". Rewrite for
    the package (four rules, now five).
  - Line 39 says the DAG "is enforced by a spec, not convention: `src/zone-dag.test.ts` resolves
    every specifier against its importer's real directory and judges the edge." That is now
    `wallow/zone-dag`. Line 38 also credits the spec with deriving its prefixes from
    `tsconfig.json` `paths` — still true, but it is the rule doing it.
  - Lines 33–34 describe `shared/` as "limited to `components`, `hooks`, `lib`, `stores`,
    `testing`, `types`". **That is no longer the policy** — delete the constraint. `shared/` is
    the zone for anything cross-feature; its subdirectories are not fixed.
- `docs/development/frontend-setup.md:118` repeats the same allowlist ("`components`, `hooks`,
  `lib`, `stores`, `testing`, `types`") in a sentence about promotion into `shared/`. Drop the
  parenthetical; keep the promotion guidance. `rg 'SHARED_SUBDIRS|sanctioned subdirector'` should
  come back empty when you are done.

**Step 2: Full gate**

```bash
pnpm check
```
Expected: green end to end.

**Step 3: E2E** — this plan touches no app source, only configs and specs, so E2E is not required.
Run it if `pnpm check` surfaces anything surprising.

**Step 4: Record the real numbers**

```bash
git diff --stat main -- 'apps/*/src/zone-dag.test.ts'
```
Put the actual line delta in the commit body rather than this plan's estimate.

**Step 5: Commit and push**

```bash
git add -A
git commit -m "docs: document the lint rule package"
git pull --rebase && bd dolt push && git push
```

**Step 6:** Close the beads. Work is not complete until `git push` succeeds and `git status` shows
the branch up to date with origin.

---

## Deferred — file beads, do not do here

- `server-only-naming`, `feature-barrels`, `client-navigation` conversions. Analysed in the design
  doc's Section B; all three are convertible, none is required for this slice.
- The `new-rule` generator, the generated rule registry, and the `wallow-lint-rule` skill. They earn
  their keep across many rules; this plan writes one. Revisit when the second or third conversion
  lands.
- The suppression census and `--report-unused-disable-directives`. Both exist to police
  `oxlint-disable` comments, and this slice adds none.
- A real `build` script and publishing. `publishConfig` is wired so it can be added without
  reshaping the package.

## Notes for the executor

- **Do not touch the root `.oxlintrc.json` `jsPlugins`.** There is no such key today and there must
  not be one. See `packages/lint/CLAUDE.md`.
- **Do not converge the two app configs.** wallow-auth forbids raw `<button>` and owns
  `text-heading-variant`; wallow-web's `bff-demo` ships four raw buttons on purpose.
- **A rule that reports nothing on the real apps has not been proved.** The fixtures are the proof;
  `pnpm lint` reporting zero only confirms the apps are currently clean.
- The `query-facade.test.ts` consolidation across three apps was already in flight in the working
  tree when this plan was written. Separate work; leave it alone.
