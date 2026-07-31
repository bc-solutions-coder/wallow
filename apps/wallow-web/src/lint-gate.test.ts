import { spawnSync, type SpawnSyncReturns } from "node:child_process";
import { type Dirent, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterAll, beforeAll, describe, expect, it } from "vitest";

/**
 * The lint gate that keeps F5's migration from regressing (Wallow-lrlm.5.6).
 *
 * F5.T1-T5 and T7 removed two patterns from this app: raw `<p>`/`<span>`/
 * `<h1>`..`<h6>` copy that decides the type scale a second time instead of
 * asking `packages/ui`'s `Text`, and the `bg-foreground`/`text-background`
 * sidebar INVERSION, which does not name a surface — it swaps the two page
 * colours, so a fork editing `branding.json` cannot reach it. The sibling
 * source specs (`src/typography.test.ts`, `src/app/typography.test.ts`,
 * `src/shared/components/dashboard-chrome-tokens.test.ts`) proved those patterns
 * are gone from the files they each sweep; this bead makes `pnpm lint` refuse
 * them ANYWHERE in the app, including in a file nobody has written yet.
 *
 * WHY THE SPEC DRIVES THE REAL LINTER. A gate that matches nothing passes just
 * as quietly as a gate that works, and this epic has already shipped one guard
 * that turned out to be inert. Asserting the shape of `.oxlintrc.json` would
 * reproduce that failure mode exactly: a rule can be spelled correctly, scoped
 * to a glob that matches no file, and still read as present. So every assertion
 * below RUNS `oxlint` — the repo's own binary, from the repo root, against the
 * repo's own config — and judges the diagnostics it emits. The rule has to fire
 * on a known-bad file before this spec calls it a gate.
 *
 * WHY FIXTURES ON DISK. oxlint resolves both its config and its per-glob
 * `overrides` relative to the config file's directory, so a snippet linted from
 * a temp directory outside the repo is judged by a DIFFERENT rule set than the
 * app is. The fixtures therefore live inside `apps/wallow-web/` — the only place
 * where what this spec measures is what `pnpm lint` measures. They are written
 * and deleted per assertion, and `__lintfixtures__/` is gitignored, which (as
 * `a stale fixture cannot break the workspace lint` pins below) is also what
 * keeps a fixture surviving a crashed run from breaking `pnpm lint` for
 * everyone.
 *
 * Node project: spawns a process and reads files, mounts nothing.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "..");
const repoRoot: string = resolve(appRoot, "../..");

/** The binary `pnpm lint` runs, invoked the same way: from the repo root. */
const oxlint: string = resolve(repoRoot, "node_modules/.bin/oxlint");

/**
 * Where a fixture is written. Inside the app (so the app's `overrides` apply),
 * outside `src/` (so vitest never tries to collect a `*.test.tsx` fixture as a
 * spec), and gitignored (so a leaked one cannot fail the workspace lint).
 */
const FIXTURE_DIR = "apps/wallow-web/__lintfixtures__";
const fixtureDir: string = resolve(repoRoot, FIXTURE_DIR);

/** One oxlint diagnostic, as `--format=json` reports it. */
interface Diagnostic {
  readonly message: string;
  readonly code: string;
  readonly severity: string;
  readonly help?: string;
  readonly filename: string;
}

interface LintRun {
  readonly exitCode: number;
  readonly diagnostics: readonly Diagnostic[];
}

/**
 * Run the linter over `paths` exactly as `pnpm lint` does — same binary, same
 * cwd, same `--deny-warnings` — and read the diagnostics back as data.
 *
 * `--deny-warnings` matters: a gate registered at `warn` still fails `pnpm
 * lint`, so a spec that only accepted errors would under-report the real gate.
 */
function lint(paths: readonly string[]): LintRun {
  const run: SpawnSyncReturns<string> = spawnSync(
    oxlint,
    [...paths, "--deny-warnings", "--format=json"],
    { cwd: repoRoot, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
  );

  expect(run.error, `oxlint could not be run: ${String(run.error)}`).toBeUndefined();

  const stdout: string = run.stdout ?? "";
  let parsed: { diagnostics?: readonly Diagnostic[] };

  try {
    parsed = JSON.parse(stdout) as { diagnostics?: readonly Diagnostic[] };
  } catch {
    throw new Error(`oxlint did not emit JSON. stdout:\n${stdout}\nstderr:\n${run.stderr ?? ""}`);
  }

  return { exitCode: run.status ?? -1, diagnostics: parsed.diagnostics ?? [] };
}

/**
 * Write `source` to `name` under the fixture directory, lint that ONE file, and
 * delete it again. The file is removed even when the assertion throws, so a red
 * run does not leave the app's lint broken behind it.
 */
function lintFixture(name: string, source: string): LintRun {
  return lintNamedFixture(fixtureName(name), source);
}

/**
 * `lintFixture` with the file name taken VERBATIM.
 *
 * `lintFixture` derives a safe name from a case title (see `fixtureName`); the
 * drift guard's `unicorn/filename-case` arm needs the opposite — a name that is
 * itself the input, because that rule judges nothing else.
 */
function lintNamedFixture(fileName: string, source: string): LintRun {
  const absolute: string = resolve(fixtureDir, fileName);

  mkdirSync(fixtureDir, { recursive: true });
  writeFileSync(absolute, source, "utf8");

  try {
    return lint([`${FIXTURE_DIR}/${fileName}`]);
  } finally {
    rmSync(absolute, { force: true });
  }
}

/**
 * Kebab-case a fixture's file name.
 *
 * Not cosmetic. This app's `overrides` block sets
 * `unicorn/filename-case: ["error", { cases: { kebabCase, pascalCase } }]`, so a
 * fixture called `inversion-a-className-literal.tsx` reports a diagnostic for its
 * NAME — which is a diagnostic all the same, and would let the "rejects ..."
 * cases below pass against a gate that does not exist. The names are derived
 * from the case titles, so this is the only thing standing between a readable
 * `it.each` label and a vacuous assertion.
 */
function fixtureName(name: string): string {
  return name
    .replaceAll(/[A-Z]/gu, (letter: string) => `-${letter.toLowerCase()}`)
    .replaceAll(/[^a-z0-9.]+/gu, "-")
    .replaceAll(/-{2,}/gu, "-")
    .replace(/^-/u, "");
}

/**
 * Assert the gate rejects `offending` BECAUSE of `because`, and not for any
 * other reason the fixture happens to carry.
 *
 * The proof is a paired run: `migrated` is the same file with only the offending
 * pattern replaced by what F5 migrated it to, linted under the SAME file name.
 * Anything the fixture trips incidentally — its name, its formatting, a rule
 * about the markup around the pattern — fires in both runs and is therefore not
 * evidence of a gate. This is not hypothetical: the first draft of this spec
 * passed three cases on `unicorn/filename-case` reporting the fixture's own
 * generated name.
 */
function expectRejected(name: string, offending: string, migrated: string, because: string): void {
  const clean: LintRun = lintFixture(name, migrated);

  expect(clean.diagnostics, `the migrated twin of ${because} must lint clean`).toEqual([]);

  const rejected: LintRun = lintFixture(name, offending);

  expect(rejected.diagnostics.length, `pnpm lint accepted ${because}`).toBeGreaterThan(0);
  expect(rejected.exitCode, `pnpm lint exited 0 on ${because}`).not.toBe(0);
}

/** The diagnostics as one readable blob, for assertion messages. */
function describeRun(run: LintRun): string {
  return run.diagnostics
    .map((entry: Diagnostic) => `${entry.code}: ${entry.message} ${entry.help ?? ""}`)
    .join("\n");
}

/** Everything the gate said, lowercased — for the "does it teach" assertions. */
function guidance(run: LintRun): string {
  return describeRun(run).toLowerCase();
}

/**
 * A `${name}` template placeholder for a fixture's source. Assembled rather than
 * written literally so this spec's own source does not trip
 * `eslint(no-template-curly-in-string)` — which would break the "passes clean
 * over the whole workspace" assertion below with a false positive of its own.
 */
function placeholder(name: string): string {
  return `\${${name}}`;
}

beforeAll(() => {
  // A previous run killed mid-assertion would otherwise leave a fixture behind
  // and make every "reports nothing" case below fail for an unrelated reason.
  rmSync(fixtureDir, { recursive: true, force: true });
});

afterAll(() => {
  rmSync(fixtureDir, { recursive: true, force: true });
});

/*
 * The harness itself, asserted before anything is asserted THROUGH it. If
 * `lintFixture` silently linted nothing — a wrong path, a binary that exits
 * before reading the file, a config that ignores the directory — every gate
 * assertion below would report zero diagnostics and the "allows" cases would
 * pass while the "rejects" cases failed for a reason that has nothing to do with
 * the gate. This case makes that failure mode loud and separate.
 */
describe("the fixture harness", () => {
  it("actually lints the fixture it writes", () => {
    const run: LintRun = lintFixture(
      "harness.tsx",
      "export function Harness() {\n  debugger;\n  return null;\n}\n",
    );

    expect(run.diagnostics.map((entry: Diagnostic) => entry.code)).toContain("eslint(no-debugger)");
    expect(run.exitCode).not.toBe(0);
  });

  it("reports the fixture's own path, not some other file's", () => {
    const run: LintRun = lintFixture(
      "attribution.tsx",
      "export function Attribution() {\n  debugger;\n  return null;\n}\n",
    );

    for (const entry of run.diagnostics) {
      expect(entry.filename).toContain("__lintfixtures__");
    }
  });

  it("reports nothing on a file that already follows the migration", () => {
    // The control. Every "rejects" case below is this file with ONE thing
    // changed, so a diagnostic there is attributable to that change and not to
    // the fixture being noisy under some unrelated rule.
    const run: LintRun = lintFixture(
      "control.tsx",
      [
        'import { Text } from "@bc-solutions-coder/ui";',
        "",
        "export function Control() {",
        "  return (",
        '    <div className="bg-sidebar text-sidebar-foreground">',
        '      <Text as="h1">Title</Text>',
        "    </div>",
        "  );",
        "}",
        "",
      ].join("\n"),
    );

    expect(run.diagnostics, describeRun(run)).toEqual([]);
    expect(run.exitCode).toBe(0);
  });

  it("a stale fixture cannot break the workspace lint", () => {
    // The fixture directory is gitignored, and oxlint skips gitignored paths
    // when it WALKS a directory while still linting them when handed one
    // explicitly. That is what makes it safe to write violations into the app's
    // own tree: `pnpm lint` never sees them, this spec always does.
    const gitignore: string = readFileSync(resolve(repoRoot, ".gitignore"), "utf8");

    expect(gitignore, ".gitignore must name the fixture directory").toContain("__lintfixtures__");

    mkdirSync(fixtureDir, { recursive: true });
    const stale: string = resolve(fixtureDir, "stale.tsx");
    writeFileSync(stale, "export function Stale() {\n  return <p>copy</p>;\n}\n", "utf8");

    try {
      const run: LintRun = lint(["apps/wallow-web"]);

      for (const entry of run.diagnostics) {
        expect(entry.filename, describeRun(run)).not.toContain("__lintfixtures__");
      }
    } finally {
      rmSync(stale, { force: true });
    }
  });
});

/**
 * The elements `Text` now owns. `legend` and `code` are deliberately absent:
 * `src/typography.test.ts` sweeps them at the source level, but this bead's
 * acceptance criteria name `p`, `span` and `h1`..`h6`, and widening a lint gate
 * past its acceptance criteria is how a gate acquires a false positive nobody
 * agreed to.
 */
const BANNED_ELEMENTS: readonly string[] = ["p", "span", "h1", "h2", "h3", "h4", "h5", "h6"];

/** One `Regression` component returning `body`. */
function component(body: string): string {
  return ["export function Regression() {", `  return ${body};`, "}", ""].join("\n");
}

describe("the lint gate rejects a reintroduced raw text element", () => {
  it.each(BANNED_ELEMENTS)("rejects <%s>", (tag: string) => {
    expectRejected(
      `element-${tag}.tsx`,
      component(`<${tag}>copy</${tag}>`),
      component("<div>copy</div>"),
      `a raw <${tag}>`,
    );
  });

  it("says what to reach for instead", () => {
    // A gate that only says "forbidden" sends the next author looking for the
    // rule rather than for `Text`. The `no-restricted-imports` entries already
    // in `.oxlintrc.json` all carry a message for exactly this reason.
    const run: LintRun = lintFixture(
      "element-guidance.tsx",
      "export function Regression() {\n  return <p>copy</p>;\n}\n",
    );

    expect(guidance(run), describeRun(run)).toContain("text");
  });
});

/**
 * The inversion, in each shape it is actually written in this app. A rule that
 * only reads `className="..."` would pass the last two — and the last two are
 * how the pattern got here: `DashboardLayout` hoists its one surviving colour
 * into a `const` and interpolates it, which is precisely the shape a
 * reintroduction would copy.
 */
const INVERSION_FIXTURES: readonly (readonly [string, string, string])[] = [
  [
    "a className literal",
    component('<div className="bg-foreground text-background" />'),
    component('<div className="bg-sidebar text-sidebar-foreground" />'),
  ],
  [
    "a variant-prefixed className",
    component('<div className="hover:bg-foreground md:text-background" />'),
    component('<div className="hover:bg-sidebar md:text-sidebar-foreground" />'),
  ],
  [
    "an interpolated className",
    [
      'const LAYER = "z-20";',
      "",
      component(`<div className={\`bg-foreground text-background ${placeholder("LAYER")}\`} />`),
    ].join("\n"),
    [
      'const LAYER = "z-20";',
      "",
      component(
        `<div className={\`bg-sidebar text-sidebar-foreground ${placeholder("LAYER")}\`} />`,
      ),
    ].join("\n"),
  ],
  [
    "a hoisted class const",
    [
      'const SURFACE = "bg-foreground text-background";',
      "",
      component("<div className={SURFACE} />"),
    ].join("\n"),
    [
      'const SURFACE = "bg-sidebar text-sidebar-foreground";',
      "",
      component("<div className={SURFACE} />"),
    ].join("\n"),
  ],
];

describe("the lint gate rejects a reintroduced sidebar inversion", () => {
  it.each(INVERSION_FIXTURES)(
    "rejects the inversion written as %s",
    (shape: string, offending: string, migrated: string) => {
      expectRejected(
        `inversion-${shape}.tsx`,
        offending,
        migrated,
        `bg-foreground/text-background written as ${shape}`,
      );
    },
  );

  it("says what to reach for instead", () => {
    const run: LintRun = lintFixture(
      "inversion-guidance.tsx",
      'export function Regression() {\n  return <div className="bg-foreground text-background" />;\n}\n',
    );

    expect(guidance(run), describeRun(run)).toContain("sidebar");
  });
});

/*
 * The other half of a gate. Everything above is satisfied by a rule that bans
 * far more than F5 removed, and an over-broad rule is not a cheaper mistake than
 * a missing one — it is the mistake that gets the whole gate switched off. Each
 * case below is a class or element the app deliberately still uses, verified on
 * disk by the beads that ruled on it.
 */
describe("the lint gate leaves the deliberate cases alone", () => {
  it("allows the drawer scrim's translucent bg-foreground/40", () => {
    // Wallow-lrlm.5.4's one carve-out, written here exactly as
    // `DashboardLayout.tsx` writes it. A scrim that is not see-through is a
    // blank page, so no opaque token can express it and `packages/ui`'s own
    // backdrop recipes use the same idiom. Translucency is categorically not an
    // inversion: an inversion swaps two OPAQUE colours.
    const run: LintRun = lintFixture(
      "scrim.tsx",
      [
        'const BACKDROP_SCRIM = "bg-foreground/40";',
        "",
        "export function Backdrop() {",
        `  return <div className={\`fixed inset-y-0 right-0 left-64 z-20 ${placeholder("BACKDROP_SCRIM")}\`} />;`,
        "}",
        "",
      ].join("\n"),
    );

    expect(run.diagnostics, describeRun(run)).toEqual([]);
  });

  it("allows the landing CTA's bare border-foreground", () => {
    // Wallow-lrlm.5.3 adjudicated this one: an outline button whose border is
    // the page's own ink is not an inversion, and `LandingPage.tsx` ships it. It
    // is named here so that widening the ban list to the whole `*-foreground`
    // family shows up as a red spec rather than as a broken landing page.
    const run: LintRun = lintFixture(
      "cta.tsx",
      'export function Cta() {\n  return <a className="border border-foreground text-foreground" href="/x" />;\n}\n',
    );

    expect(run.diagnostics, describeRun(run)).toEqual([]);
  });

  it("allows the page's own colours", () => {
    // `bg-background` and `text-foreground` are the page painting itself, not
    // swapping. Only the pair that means "swap them" is retired.
    const run: LintRun = lintFixture(
      "page-colours.tsx",
      'export function Page() {\n  return <div className="bg-background text-foreground" />;\n}\n',
    );

    expect(run.diagnostics, describeRun(run)).toEqual([]);
  });

  it.each(["div", "button", "pre", "li", "label", "a"])(
    "allows the layout and control element <%s>",
    (tag: string) => {
      // `Text` replaced COPY, not markup. `bff-demo.tsx` keeps its `<pre>` and
      // its buttons for exactly this reason (Wallow-lrlm.5.7).
      const run: LintRun = lintFixture(
        `allowed-${tag}.tsx`,
        `export function Allowed() {\n  return <${tag} />;\n}\n`,
      );

      expect(run.diagnostics, describeRun(run)).toEqual([]);
    },
  );

  it("allows a spec to render raw markup as a fixture", () => {
    // `PublicLayout.test.tsx` mounts a raw `<p data-testid="public-body-probe">`
    // as a slot probe. A spec's fixture markup is not this app's copy, and the
    // existing `**/*.test.*` override block is where the repo already says so.
    const run: LintRun = lintFixture(
      "exempt.test.tsx",
      'export function Probe() {\n  return <p data-testid="probe">body</p>;\n}\n',
    );

    expect(run.diagnostics, describeRun(run)).toEqual([]);
  });
});

/*
 * The gate against the tree it guards. The two assertions the acceptance
 * criteria state directly — lint is clean today — plus the scope constraint,
 * which is the one that cannot be checked from inside `apps/wallow-web`:
 * `packages/ui` renders the very elements this bans and paints an animated
 * backdrop with a BARE `bg-foreground`, so a gate that reached the workspace
 * would break the catalog it exists to protect.
 */
describe("the lint gate against the shipped tree", () => {
  it("passes clean over the whole workspace", () => {
    const run: LintRun = lint(["apps", "packages"]);

    expect(run.diagnostics, describeRun(run)).toEqual([]);
    expect(run.exitCode).toBe(0);
  });

  it("leaves packages/ui's own text primitives and backdrops alone", () => {
    const run: LintRun = lint(["packages/ui"]);

    expect(run.diagnostics, describeRun(run)).toEqual([]);

    // Not vacuous: the catalog still contains the very things the gate bans, so
    // a clean result means the gate is SCOPED, not that the classes are gone.
    const drawer: string = readFileSync(
      resolve(repoRoot, "packages/ui/src/components/drawer/drawer.styles.ts"),
      "utf8",
    );

    expect(drawer, "the animated backdrop's bare bg-foreground must still be there").toMatch(
      /\bbg-foreground\b(?!\/)/u,
    );
  });

  it("leaves the MFA panels alone", () => {
    // Wallow-lrlm.5.5 (ruling J2) deliberately did NOT migrate these two onto
    // `useAppForm` — they are an input-with-action, not a submitted form, and
    // the catalog has no answer for that shape yet (follow-up Wallow-6uqv).
    // Whatever this gate bans must not make that deferral un-lintable.
    const run: LintRun = lint([
      "apps/wallow-web/src/features/mfa/components/MfaEnrollFlow.tsx",
      "apps/wallow-web/src/features/mfa/components/MfaSettingsSection.tsx",
    ]);

    expect(run.diagnostics, describeRun(run)).toEqual([]);
  });

  it("does not disturb the source guards that already ban these patterns", () => {
    // `dashboard-chrome-tokens.test.ts` reads the same two class names out of the
    // chrome files and keeps the same single carve-out. The lint rule has to
    // AGREE with it, so its carve-out literal is pinned here too: if this gate
    // ever talks the other spec into deleting its exemption, the scrim silently
    // becomes bannable and the drawer goes opaque.
    const chromeGuard: string = readFileSync(
      resolve(appRoot, "src/shared/components/dashboard-chrome-tokens.test.ts"),
      "utf8",
    );

    expect(chromeGuard).toContain('const BACKDROP_SCRIM = "bg-foreground/40"');
    expect(readFileSync(resolve(appRoot, "src/typography.test.ts"), "utf8")).toContain(
      "SIDEBAR_INVERSION",
    );
  });
});
/**
 * The subset of an oxlint config this guard reads.
 *
 * Only the two things the nested config is forced to RESTATE — see the block
 * comment below. Everything else about the config is judged by running the
 * binary, not by reading the file.
 */
interface OverrideBlock {
  readonly files?: readonly string[];
  readonly rules?: Readonly<Record<string, unknown>>;
}

interface OxlintConfig {
  readonly ignorePatterns?: readonly string[];
  readonly overrides?: readonly OverrideBlock[];
}

function readOxlintConfig(configPath: string): OxlintConfig {
  return JSON.parse(readFileSync(configPath, "utf8")) as OxlintConfig;
}

const rootConfig: OxlintConfig = readOxlintConfig(resolve(repoRoot, ".oxlintrc.json"));
const childConfig: OxlintConfig = readOxlintConfig(resolve(appRoot, ".oxlintrc.json"));

/** This app, as a glob in the ROOT config has to spell it. */
const APP_DIR = "apps/wallow-web";

/**
 * Split `a,b{c,d}` on its TOP-LEVEL commas only, so a nested alternation stays
 * with the alternative it belongs to.
 */
function splitAlternatives(body: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let current = "";

  for (const character of body) {
    if (character === "," && depth === 0) {
      parts.push(current);
      current = "";
    } else {
      if (character === "{") {
        depth += 1;
      } else if (character === "}") {
        depth -= 1;
      }

      current += character;
    }
  }

  parts.push(current);

  return parts;
}

/**
 * Expand `{a,b}` alternation into the concrete globs it stands for.
 *
 * `undefined` when the braces do not balance — a spelling this matcher cannot
 * model has to be REPORTED, never quietly dropped. See
 * `understands every glob spelling the two configs use`.
 */
function expandBraces(glob: string): string[] | undefined {
  const open: number = glob.indexOf("{");

  if (open === -1) {
    return glob.includes("}") ? undefined : [glob];
  }

  let depth = 0;
  let close = -1;

  for (let index = open; index < glob.length; index += 1) {
    if (glob[index] === "{") {
      depth += 1;
    } else if (glob[index] === "}") {
      depth -= 1;

      if (depth === 0) {
        close = index;
        break;
      }
    }
  }

  if (close === -1) {
    return undefined;
  }

  const head: string = glob.slice(0, open);
  const tail: string = glob.slice(close + 1);
  const expanded: string[] = [];

  for (const alternative of splitAlternatives(glob.slice(open + 1, close))) {
    const rest: string[] | undefined = expandBraces(`${head}${alternative}${tail}`);

    if (rest === undefined) {
      return undefined;
    }

    expanded.push(...rest);
  }

  return expanded;
}

/**
 * The characters this matcher will carry through literally.
 *
 * A WHITELIST on purpose. Everything outside it — a character class, an extglob,
 * a leading `!` negation — is glob syntax whose meaning this guard does not
 * model, and the one thing it must never do with a spelling it does not
 * understand is ignore it.
 */
const LITERAL_GLOB_CHARACTER = /[\w./-]/u;

/** One brace-free glob as a RegExp, or `undefined` if it uses syntax not modelled here. */
function globToRegExp(glob: string): RegExp | undefined {
  let pattern = "";
  let index = 0;

  while (index < glob.length) {
    const rest: string = glob.slice(index);
    const character: string = rest[0] ?? "";

    if (rest.startsWith("**/")) {
      // `**/` spans zero or more WHOLE segments; a trailing `**` spans the rest.
      pattern += "(?:[^/]*/)*";
      index += 3;
    } else if (rest.startsWith("**")) {
      pattern += ".*";
      index += 2;
    } else if (character === "*") {
      pattern += "[^/]*";
      index += 1;
    } else if (character === "?") {
      pattern += "[^/]";
      index += 1;
    } else if (LITERAL_GLOB_CHARACTER.test(character)) {
      pattern += character === "." ? String.raw`\.` : character;
      index += 1;
    } else {
      return undefined;
    }
  }

  return new RegExp(`^${pattern}$`, "u");
}

const compiledGlobs = new Map<string, readonly RegExp[] | undefined>();

/** Every expansion of `glob`, compiled — or `undefined` if any part is unmodelled. */
function compileGlob(glob: string): readonly RegExp[] | undefined {
  if (!compiledGlobs.has(glob)) {
    const expansions: string[] | undefined = expandBraces(glob);
    const compiled: RegExp[] = [];
    let modelled: boolean = expansions !== undefined;

    for (const expansion of expansions ?? []) {
      const pattern: RegExp | undefined = globToRegExp(expansion);

      if (pattern === undefined) {
        modelled = false;
        break;
      }

      compiled.push(pattern);
    }

    compiledGlobs.set(glob, modelled ? compiled : undefined);
  }

  return compiledGlobs.get(glob);
}

function globMatches(glob: string, path: string): boolean {
  return (compileGlob(glob) ?? []).some((pattern: RegExp): boolean => pattern.test(path));
}

/**
 * The rules oxlint ends up applying to `path`, folding `blocks` in order.
 *
 * LAST MATCHING BLOCK WINS — that is oxlint's own resolution order, and the
 * reason this guard cannot ask "does SOME block say the right thing". A later
 * block naming an overlapping glob switches a restated rule back off with
 * nothing to show for it: appending `{"files":["**\/*.{ts,tsx}"],"rules":
 * {"unicorn/filename-case":"off"}}` to the nested config makes a `BAD_Name.ts`
 * lint silently clean while every block still "names the glob with the right
 * value" somewhere above it.
 */
function effectiveRules(
  blocks: readonly OverrideBlock[],
  path: string,
): Readonly<Record<string, unknown>> {
  const applied: Record<string, unknown> = {};

  for (const block of blocks) {
    if ((block.files ?? []).some((glob: string): boolean => globMatches(glob, path))) {
      Object.assign(applied, block.rules ?? {});
    }
  }

  return applied;
}

/** Build directories that hold no source this app is linted on. */
const UNLINTED_DIRECTORIES: ReadonlySet<string> = new Set([
  "node_modules",
  "dist",
  "test-results",
  "playwright-report",
  "__lintfixtures__",
  "__screenshots__",
]);

/** Every `.ts`/`.tsx` file in this app, spelled relative to the app root. */
function appSources(directory: string, prefix: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry: Dirent): string[] => {
    const path: string = prefix === "" ? entry.name : `${prefix}/${entry.name}`;

    if (entry.isDirectory()) {
      return entry.name.startsWith(".") || UNLINTED_DIRECTORIES.has(entry.name)
        ? []
        : appSources(resolve(directory, entry.name), path);
    }

    return /\.tsx?$/u.test(entry.name) ? [path] : [];
  });
}

const APP_FILES: readonly string[] = appSources(appRoot, "");

/** Every glob either config writes, so an unmodelled spelling can be named. */
const DECLARED_GLOBS: readonly string[] = [
  ...(rootConfig.ignorePatterns ?? []),
  ...(childConfig.ignorePatterns ?? []),
  ...(rootConfig.overrides ?? []).flatMap(
    (block: OverrideBlock): readonly string[] => block.files ?? [],
  ),
  ...(childConfig.overrides ?? []).flatMap(
    (block: OverrideBlock): readonly string[] => block.files ?? [],
  ),
];

/** A concrete path the glob matches, built by filling its wildcards in. */
function concretize(glob: string): string {
  return glob
    .replaceAll("**/", "")
    .replaceAll("**", "any")
    .replaceAll("*", "any")
    .replaceAll("?", "x");
}

interface RequiredIgnore {
  /** The root pattern, as the root spells it. */
  readonly pattern: string;
  /** A path inside this app that it covers, spelled relative to the app root. */
  readonly path: string;
}

/**
 * Each root ignore pattern that reaches INSIDE this app, with a path it covers.
 *
 * Derived by construction rather than by recognising prefixes: fill the pattern's
 * wildcards in — once as written, once with its leading `apps/<segment>/` taken
 * to BE this app, which is what makes `apps/*\/…` and `apps/**\/…` land here too —
 * and keep a candidate only when the pattern itself matches the resulting path
 * under `apps/wallow-web/`. A pattern anchored somewhere else
 * (`packages/sdk/src/generated/**`, `apps/wallow-auth/**`) fails that test on its
 * own terms, so nothing has to be spelled in a prefix list this guard can fall
 * behind.
 */
const REQUIRED_IGNORES: readonly RequiredIgnore[] = (rootConfig.ignorePatterns ?? []).flatMap(
  (pattern: string): RequiredIgnore[] =>
    (expandBraces(pattern) ?? []).flatMap((expansion: string): RequiredIgnore[] => {
      const candidates: string[] = [concretize(expansion)];

      if (expansion.startsWith("apps/")) {
        candidates.push(concretize(expansion.split("/").slice(2).join("/")));
      }

      const covered: string | undefined = candidates.find((candidate: string): boolean =>
        globMatches(expansion, `${APP_DIR}/${candidate}`),
      );

      return covered === undefined ? [] : [{ path: covered, pattern: expansion }];
    }),
);

/*
 * The nested config against the root it extends.
 *
 * WHY THERE IS A NESTED CONFIG. The class-string half of this gate can only be
 * expressed as a JS plugin, and a `jsPlugins` entry in the ROOT config takes
 * `packages/sdk/src/oxlint-guardrails.test.ts` down — that spec proves the root's
 * import bans by copying the root config into a temp directory, where no
 * `jsPlugins` specifier resolves, so oxlint refuses the config and the whole file
 * fails to collect. Top-level and override-scoped `jsPlugins` were both tried and
 * both fail that way. `tools/oxlint/wallow-lint-plugin.js`'s header has the full
 * account.
 *
 * WHAT THAT COSTS. oxlint matches an override's `files` and a config's
 * `ignorePatterns` relative to the CONFIG FILE's directory, so the root's
 * `apps/wallow-web/**` block and its repo-rooted ignore patterns stop matching the
 * moment this app is linted through `apps/wallow-web/.oxlintrc.json`. The nested
 * config restates them. A restatement is a copy, and a copy drifts: an edit to the
 * root's wallow-web block silently stops applying here, with nothing failing. That
 * is not hypothetical — adding `eqeqeq: "error"` to the root's wallow-web block
 * leaves a wallow-web file at WARNING (the category default), while the same edit
 * to the root's wallow-auth block makes a wallow-auth file ERROR.
 *
 * HOW THE COMPARISON IS MADE, AND WHY NOT BY READING GLOBS. This guard resolves
 * both configs the way oxlint does — match each override's globs against a real
 * PATH, fold the matches in order, last one wins — and compares, for every file
 * in this app, the rules the root declares for it against the rules linting it
 * through the nested config actually applies. Nothing recognises a glob by its
 * prefix, because a classifier that keys on spellings silently drops the ones it
 * has not been taught, one level up from the invisible non-application this guard
 * exists to make visible. Two spellings that beat exactly such a classifier:
 * a root block globbed `apps/{wallow-web,wallow-auth}/**\/*.{ts,tsx}` (real, and
 * a likely tidy-up, since the root carries three byte-identical app blocks), and
 * a nested block appended AFTER the restatement that turns the restated rules
 * back off. Both are caught here as ordinary path resolution. The one thing that
 * cannot be resolved is a glob whose syntax is not modelled, and the first case
 * below fails loudly on exactly that rather than skipping it.
 *
 * WHY THIS BLOCK READS CONFIG TEXT WHEN THE REST OF THE FILE REFUSES TO. Every
 * other assertion here runs the binary, because the failure mode there is "the
 * rule matches nothing". The failure mode HERE is different in kind: the root says
 * something and the child is SILENT about it, and silence has no diagnostic to
 * observe. Measuring it behaviourally would mean mutating the root config
 * mid-run, which races `pnpm lint` and can leak a broken config. So the
 * comparison is textual — and the last case below pins the half that text cannot
 * prove, that the child's restatement is genuinely what carries those rules here.
 *
 * Root TOP-LEVEL `rules` are deliberately NOT compared: `extends` does carry them
 * down (a wallow-web file importing `@tanstack/react-query` still errors on
 * `no-restricted-imports`), so they are covered by "passes clean over the whole
 * workspace" above. Root OVERRIDES are folded into the applied side too, because
 * the ones with no repo-rooted prefix do still reach here through `extends` (a
 * wallow-web `*.test.ts` with `await` in a loop is silent, from the root's
 * `**\/*.test.*` block).
 */
describe("the nested oxlint config does not drift from the root", () => {
  it("understands every glob spelling the two configs use", () => {
    // The loud default. Every case below resolves globs against paths; one this
    // matcher cannot model would resolve against nothing and quietly stop
    // comparing whatever it governs. That is the inert-guard failure this epic
    // has already shipped once, so an unknown spelling fails HERE instead.
    for (const glob of DECLARED_GLOBS) {
      expect(
        compileGlob(glob),
        `this guard cannot model the glob \`${glob}\`, so it would silently stop comparing what that block declares for this app. Teach \`globToRegExp\`/\`expandBraces\` the spelling — do not leave it unclassified`,
      ).toBeDefined();
    }
  });

  it("finds the root declarations this app has to restate", () => {
    // Non-vacuity. Everything below iterates lists DERIVED from the root config
    // and this app's tree, so an emptied root block or a file walk that found
    // nothing would make the whole describe pass by comparing nothing. Name what
    // the derivation has to have found.
    expect(APP_FILES, "the file walk found no source in this app").toContain(
      "src/shared/components/DashboardLayout.tsx",
    );
    expect(APP_FILES).toContain("src/lint-gate.test.ts");

    expect(
      Object.keys(effectiveRules(rootConfig.overrides ?? [], `${APP_DIR}/src/aliases.ts`)),
      "the root config declares no rules for a plain source file in this app, so comparing its declarations proves nothing — a renamed, emptied or re-globbed root block must not pass this guard by leaving it with nothing to iterate",
    ).toEqual(expect.arrayContaining(["unicorn/filename-case", "no-magic-numbers"]));

    expect(REQUIRED_IGNORES.map((entry: RequiredIgnore): string => entry.pattern)).toContain(
      "**/routeTree.gen.ts",
    );

    // …and the reach test is not simply "everything": a root pattern anchored at
    // another package must NOT be demanded of this app's config.
    expect(REQUIRED_IGNORES.map((entry: RequiredIgnore): string => entry.pattern)).not.toContain(
      "packages/sdk/src/generated/**",
    );
  });

  it("carries every rule the root declares for this app, resolved the way oxlint resolves them", () => {
    const rootBlocks: readonly OverrideBlock[] = rootConfig.overrides ?? [];
    const childBlocks: readonly OverrideBlock[] = childConfig.overrides ?? [];

    // One assertion per DISTINCT (rule, root value, applied value) triple, with
    // the first file that produced it — 260-odd files would otherwise repeat the
    // same finding a hundred times over.
    const findings = new Map<
      string,
      { path: string; rule: string; declared: unknown; applied: unknown }
    >();

    for (const path of APP_FILES) {
      const declared: Readonly<Record<string, unknown>> = effectiveRules(
        rootBlocks,
        `${APP_DIR}/${path}`,
      );
      const applied: Readonly<Record<string, unknown>> = effectiveRules(
        [...rootBlocks, ...childBlocks],
        path,
      );

      for (const [rule, value] of Object.entries(declared)) {
        const key = `${rule} ${JSON.stringify(value)} ${JSON.stringify(applied[rule] ?? null)}`;

        if (!findings.has(key)) {
          findings.set(key, { applied: applied[rule], declared: value, path, rule });
        }
      }
    }

    for (const finding of findings.values()) {
      expect(
        finding.applied,
        `the root config sets \`${finding.rule}\` for \`${APP_DIR}/${finding.path}\`, but resolving that file through apps/wallow-web/.oxlintrc.json lands somewhere else, so the root's value silently stops applying here. Restate it in the nested config — and note oxlint takes the LAST matching override, so a later block can undo a restatement that is spelled correctly further up`,
      ).toEqual(finding.declared);
    }
  });

  it("ignores every path inside this app that the root's ignorePatterns ignore", () => {
    // A dropped ignore pattern does not fail loudly either — it makes the linter
    // start reading generated output (`routeTree.gen.ts`, `.output/`) and blame
    // this app for what it finds there.
    const childIgnores: readonly string[] = childConfig.ignorePatterns ?? [];

    expect(REQUIRED_IGNORES.length).toBeGreaterThan(0);

    for (const { pattern, path } of REQUIRED_IGNORES) {
      expect(
        childIgnores.some((candidate: string): boolean => globMatches(candidate, path)),
        `the root ignores \`${pattern}\`, which covers \`${path}\` inside this app, and no ignorePatterns entry in apps/wallow-web/.oxlintrc.json covers it`,
      ).toBe(true);
    }
  });

  it("applies the restated block to this app's files in practice", () => {
    // The half a text comparison cannot reach: a child block that is itself inert
    // would satisfy every case above. Each arm measures ONE restated option on the
    // real binary, and each is chosen to distinguish "restated" from "off" — the
    // shape a later override block silently produces.

    // `unicorn/filename-case`. The file NAME is the whole input, so this arm needs
    // the verbatim helper: a snake_case name is neither kebab nor Pascal.
    const named: LintRun = lintNamedFixture("bad_Name.ts", 'export const value = "ok";\n');

    expect(
      named.diagnostics.some((entry: Diagnostic): boolean => entry.code.includes("filename-case")),
      describeRun(named),
    ).toBe(true);

    // `no-magic-numbers`, both arms. `1` is silent under the root's
    // `{ ignore: [0, 1] }` AND under `off`, so it proves the option is carried
    // only together with a value that is NOT ignored.
    const ignored: LintRun = lintFixture(
      "restated-option.ts",
      "export function add(n: number): number {\n  return n + 1;\n}\n",
    );

    expect(ignored.diagnostics, describeRun(ignored)).toEqual([]);

    const magic: LintRun = lintFixture(
      "restated-option.ts",
      "export function add(n: number): number {\n  return n + 42;\n}\n",
    );

    expect(
      magic.diagnostics.some((entry: Diagnostic): boolean =>
        entry.code.includes("no-magic-numbers"),
      ),
      describeRun(magic),
    ).toBe(true);
  });
});
