import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { stripComments } from "@shared/testing/strip-comments";
import { describe, expect, it } from "vitest";

/**
 * The app-zone half of the typography sweep (Wallow-lrlm.5.7).
 *
 * `src/typography.test.ts` (Wallow-lrlm.5.3) judges `features/*\/components` and
 * `shared/components` — precisely the two directories 5.3's acceptance criteria
 * named — so the THIRD zone, `src/app`, was never swept. `bff-demo.tsx` is what
 * survived there: an unstyled `<h1>`, two `<p>`s and two `<span>`s, the last raw
 * text elements in this app.
 *
 * This spec is the app-zone counterpart, and it is DISK-DERIVED for the same
 * reason 5.3's is: this epic has repeatedly shipped a file no inventory
 * mentioned, so every non-test `.tsx` under `src/app` is judged whether or not
 * anyone remembered it. `KNOWN_APP_SOURCES` exists only to prove the walk is not
 * vacuous. `routeTree.gen.ts` is generated and is never read here.
 *
 * The second half pins `bff-demo.tsx` specifically, because the sweep alone
 * cannot tell a correct migration from a deletion. Three things have to hold:
 *
 *   1. Each of the five sites keeps its ELEMENT, via `Text`'s `as`. `as` is what
 *      makes `<Text>` a heading rather than a paragraph, and it is also what
 *      derives the type scale (`h1` -> `display`, `p`/`span` -> `body`).
 *   2. Every `bff-*` `data-testid` stays on the element it sits on today, with
 *      the same spelling. This route is wallow-web's only backend-free E2E
 *      reachability route (`e2e/routes.spec.ts`) and `bff-demo.test.tsx` drives
 *      the whole id set; both must keep passing untouched.
 *   3. The migration takes `Text`'s DEFAULTS. This is a demo harness that ships
 *      no styling at all, so giving it the catalog's scale is the point — no
 *      `variant` override and no hand-written Tailwind added to "preserve" the
 *      browser defaults it happens to render with today.
 *
 * `<main>`, the four `<button>`s and the two `<pre>`s are deliberately NOT in
 * scope: `pre` is preformatted output rather than copy, is not a `Text` `as`
 * value, and was not in 5.3's element set. They are pinned below as invariants
 * so the sweep cannot be satisfied by rewriting them too.
 *
 * Sources are read with their COMMENTS STRIPPED, the same call 5.3's sweep
 * makes: a doc comment describing markup a file no longer hand-rolls is worth
 * keeping, and `bff-demo.tsx`'s header comment documents the testid contract.
 */

const appDir: URL = new URL("./", import.meta.url);

function read(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, appDir)), "utf8");
}

/** Generated output, never authored — excluded from the walk by name. */
const GENERATED: ReadonlySet<string> = new Set(["routeTree.gen.ts"]);

/**
 * Every non-test, non-generated `.tsx` under `src/app`, recursively, as an
 * app-relative path. Vitest's `__screenshots__` directories hold attachment
 * folders whose names end in `.test.tsx`; the `isFile()` filter drops them.
 */
function appSources(directory: string = ""): string[] {
  return readdirSync(fileURLToPath(new URL(directory === "" ? "./" : directory, appDir)), {
    withFileTypes: true,
  }).flatMap((entry) => {
    const path = `${directory}${entry.name}`;

    if (entry.isDirectory()) {
      return entry.name === "__screenshots__" ? [] : appSources(`${path}/`);
    }

    const authored =
      entry.isFile() &&
      entry.name.endsWith(".tsx") &&
      !entry.name.includes(".test.") &&
      !GENERATED.has(entry.name);

    return authored ? [path] : [];
  });
}

const SWEPT: readonly string[] = appSources();

/**
 * The app-zone sources on disk when this bead was written. The walk above is
 * what judges the app; this list only fails when the walk stops finding files it
 * used to find, so a broken recursion shows up as a red spec rather than as a
 * sweep that silently passes over nothing.
 */
const KNOWN_APP_SOURCES: readonly string[] = [
  "router.tsx",
  "routes/__root.tsx",
  "routes/bff-demo.tsx",
  "routes/dashboard/apps/index.tsx",
  "routes/dashboard/apps/register.tsx",
  "routes/dashboard/inquiries/$inquiryId.tsx",
  "routes/dashboard/inquiries/index.tsx",
  "routes/dashboard/organizations/$orgId.tsx",
  "routes/dashboard/organizations/index.tsx",
  "routes/dashboard/route.tsx",
  "routes/dashboard/settings.tsx",
  "routes/index.tsx",
];

/**
 * The file with its comments removed and nothing else — the SAME scan
 * `src/typography.test.ts` reads through, which is why it lives in
 * `@shared/testing` rather than being restated here. Both zones are swept
 * through one stripper, so a fix to one cannot miss the other.
 */
function code(relativePath: string): string {
  return stripComments(read(relativePath));
}

/**
 * The stripper's adversarial inputs (Wallow-lrlm.14), shared with
 * `src/typography.test.ts` because both zones read their sources through the
 * same stripper and a fix to one that misses the other leaves half this app
 * unguarded. Each fixture pairs a genuine comment (`GENUINE-COMMENT`, which must
 * be removed) with real source (`data-testid="<name>-survives"`, which must not
 * be). See the directory's README.
 */
const FIXTURE_DIR = "../__fixtures__/comment-stripper/";

/** Every stripper fixture on disk, by name. */
function fixtureNames(): string[] {
  return readdirSync(fileURLToPath(new URL(FIXTURE_DIR, appDir)), { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith(".txt"))
    .map((entry) => entry.name.replace(/\.txt$/u, ""))
    .toSorted();
}

const STRIPPER_FIXTURES: readonly string[] = fixtureNames();

/**
 * The fixtures on disk when this bead was written. As with `KNOWN_APP_SOURCES`,
 * this list exists only so deleting a fixture reads as a red spec rather than as
 * a guard that quietly stopped covering the input that used to break it.
 */
const KNOWN_STRIPPER_FIXTURES: readonly string[] = [
  "apostrophe-in-comment",
  "block-open-in-regex-literal",
  "code-in-block-comment",
  "comment-markers-in-strings",
  "glob-in-line-comment",
  "slashes-in-regex-literal",
  "unterminated-block-comment",
  "url-in-jsx-text",
  "url-in-string",
  "url-in-template-literal",
  "violation-behind-glob",
];

/** A fixture read through the very stripper every assertion below reads through. */
function strippedFixture(name: string): string {
  return code(`${FIXTURE_DIR}${name}.txt`);
}

/** A JSX opener for `tag`, however the element wraps across lines. */
function opener(tag: string): RegExp {
  return new RegExp(String.raw`<${tag}(?=[\s/>\n])`, "u");
}

/** Every occurrence of `pattern` in `source`. */
function countOf(source: string, pattern: RegExp): number {
  return source.match(pattern)?.length ?? 0;
}

/**
 * The elements whose job `Text` now does — the same set `src/typography.test.ts`
 * sweeps, so both zones are judged by one rule.
 */
const TEXT_ELEMENTS: readonly string[] = [
  "p",
  "span",
  "h1",
  "h2",
  "h3",
  "h4",
  "h5",
  "h6",
  "legend",
  "code",
];

const BFF_DEMO = "routes/bff-demo.tsx";

const IMPORTS_TEXT = /import\s*\{[^}]*\bText\b[^}]*\}\s*from\s*"@bc-solutions-coder\/ui"/u;

/**
 * The full `bff-*` contract `bff-demo.test.tsx` loops over and the C# BFF flow
 * tests drive. Restated here so the sweep cannot be satisfied by deleting a
 * surface instead of migrating it.
 */
const BFF_TESTIDS: readonly string[] = [
  "bff-user-status",
  "bff-user-email",
  "bff-login",
  "bff-logout",
  "bff-call-api",
  "bff-mutate",
  "bff-api-result",
  "bff-mutate-result",
];

/** The `<Text>` opener carrying `data-testid="{testId}"`, or `null`. */
function textOpenerFor(source: string, testId: string): string | null {
  const opening = source.match(
    new RegExp(String.raw`<Text\b[^>]*\bdata-testid="${testId}"[^>]*>`, "u"),
  );

  return opening?.[0] ?? null;
}

/**
 * The guard on the guard.
 *
 * Every assertion in this file judges `code(file)`, never the file. So the
 * stripper decides what the sweep is allowed to see, and a stripper that deletes
 * more than a comment turns the whole sweep green over source it never read —
 * indistinguishable, from the outside, from an app zone that is genuinely clean.
 *
 * The contract is one sentence: **comments are removed and nothing else is.**
 * Over-deletion is the dangerous direction — it hides violations. Under-deletion
 * only produces a noisy red, which fixes itself.
 */
describe("the comment stripper this sweep reads through", () => {
  it("reads every fixture on disk", () => {
    expect(STRIPPER_FIXTURES).toEqual(expect.arrayContaining([...KNOWN_STRIPPER_FIXTURES]));
    expect(STRIPPER_FIXTURES.length).toBeGreaterThanOrEqual(KNOWN_STRIPPER_FIXTURES.length);
  });

  it.each(STRIPPER_FIXTURES)("keeps the real source beside %s", (fixture) => {
    const stripped: string = strippedFixture(fixture);

    expect(stripped, `${fixture}: this construct is not a comment, so nothing here is`).toContain(
      `data-testid="${fixture}-survives"`,
    );
  });

  it.each(STRIPPER_FIXTURES)("still removes the genuine comment in %s", (fixture) => {
    // The other half of the contract: a stripper that stripped nothing would
    // satisfy every assertion above without stripping anything.
    expect(strippedFixture(fixture), `${fixture}: a real comment must still go`).not.toContain(
      "GENUINE-COMMENT",
    );
  });

  /*
   * The end-to-end half. The two above pin the stripper; this one pins the SWEEP
   * that reads through it — a raw `<h1>` sitting behind a line comment that names
   * a route glob is exactly the violation this zone exists to catch.
   */
  it("still catches a text element the glob only appears to hide", () => {
    const source: string = strippedFixture("violation-behind-glob");
    const found: string[] = TEXT_ELEMENTS.filter((tag) => opener(tag).test(source));

    expect(found, "the element sweep must judge what the comment appeared to hide").toContain("h1");
  });
});

describe("wallow-web's app zone renders its copy through the catalog's Text", () => {
  it("sweeps every authored source under src/app", () => {
    expect(SWEPT).toEqual(expect.arrayContaining([...KNOWN_APP_SOURCES]));
    expect(SWEPT.length).toBeGreaterThanOrEqual(KNOWN_APP_SOURCES.length);
  });

  it("reads no generated route tree", () => {
    expect(SWEPT.filter((file) => file.includes("routeTree.gen"))).toEqual([]);
  });

  it.each(SWEPT)("hand-rolls no text element in %s", (file) => {
    const source = code(file);
    const found = TEXT_ELEMENTS.filter((tag) => opener(tag).test(source));

    expect(found, `${file} must render its copy through Text, not raw elements`).toEqual([]);
  });
});

describe("bff-demo migrates its five text sites onto Text", () => {
  it("imports Text from the catalog", () => {
    expect(read(BFF_DEMO), "the demo renders copy, so it must import Text").toMatch(IMPORTS_TEXT);
  });

  it("keeps each site's element via `as`", () => {
    const source = code(BFF_DEMO);

    // The heading, the two label paragraphs, and the two value spans — five
    // sites, each keeping the element it ships today.
    expect(countOf(source, /\bas="h1"/gu), "the page title stays an h1").toBe(1);
    expect(countOf(source, /\bas="p"/gu), "both label lines stay paragraphs").toBe(2);
    expect(countOf(source, /\bas="span"/gu), "both value slots stay spans").toBe(2);
  });

  it("takes Text's derived type scale rather than restating one", () => {
    const source = code(BFF_DEMO);

    // `as` alone derives the scale (h1 -> display, p/span -> body). This page
    // ships no styling today, so adopting the catalog's scale IS the migration;
    // a `variant` override or a hand-written utility would be re-deciding it.
    expect(source, "no site names a variant").not.toMatch(/\bvariant=/u);
    expect(source, "no hand-written Tailwind is added").not.toMatch(/\bclassName=/u);
  });
});

describe("bff-demo keeps the bff-* testid contract exactly where it is", () => {
  it.each(BFF_TESTIDS)("still renders %s", (testId) => {
    expect(read(BFF_DEMO)).toContain(`data-testid="${testId}"`);
  });

  it.each(["bff-user-status", "bff-user-email"])("carries %s on a Text span", (testId) => {
    const opening = textOpenerFor(code(BFF_DEMO), testId);

    expect(opening, `${testId} must sit on the Text that replaced its span`).not.toBeNull();
    expect(opening, `${testId}'s element must still be a span`).toContain('as="span"');
  });
});

describe("bff-demo leaves the non-copy elements alone", () => {
  /*
   * The exclusions the bead draws deliberately. `pre` is preformatted output,
   * not copy: it is not a `Text` `as` value and was not in F5.T3's element set.
   * `main` is layout and the four actions are still raw buttons. Pinned so the
   * sweep above cannot be satisfied by rewriting these too.
   */
  it("prints both result surfaces in a raw pre", () => {
    const source = code(BFF_DEMO);

    expect(source).toContain('<pre data-testid="bff-api-result"');
    expect(source).toContain('<pre data-testid="bff-mutate-result"');
  });

  it("keeps the layout element and the four raw buttons", () => {
    const source = code(BFF_DEMO);

    expect(opener("main").test(source), "main is layout, not copy").toBe(true);
    expect(countOf(source, /<button(?=[\s/>\n])/gu), "all four actions stay buttons").toBe(4);
  });
});
