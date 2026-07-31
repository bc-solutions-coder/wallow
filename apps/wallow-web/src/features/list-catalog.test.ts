import { existsSync, readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The cross-feature half of F5.T2 (Wallow-lrlm.5.2) — the invariants no single
 * component's spec can see.
 *
 * Each `*.restyle.test.tsx` asserts what ITS component renders; nothing there
 * can say "and no OTHER feature still hand-rolls the same markup". These guards
 * read the feature sources off disk (the shape `src/app/routes/dashboard/
 * page-shell.test.ts` established for F5.T1) and pin the three rules the
 * migration establishes:
 *
 *   1. Every card-wrapped list is a `ListCard` + `ListRow`; no feature file
 *      writes the list-card surface string, the row cell string, or a raw
 *      `<ul>`/`<li>` of its own.
 *   2. Every list's is-empty branch is an `EmptyState`; no feature file writes
 *      the centered emoji-card string.
 *   3. Every status/role chip is a `Badge`; the chip class string appears
 *      NOWHERE under `src/features`.
 *
 * The sweeps are DERIVED from the directory rather than listed, so a feature
 * file nobody remembered still gets judged — the failure mode this epic has hit
 * twice is an inventory that missed a component nested inside another file.
 *
 * F5.T6 turns these rules into lint; until it lands this spec is the gate.
 */

const featuresDir: URL = new URL("./", import.meta.url);

/** The `.tsx` components a single feature ships, spec files excluded. */
function componentsOf(feature: string): string[] {
  const componentsUrl = new URL(`${feature}/components/`, featuresDir);
  if (!existsSync(fileURLToPath(componentsUrl))) {
    return [];
  }
  return readdirSync(fileURLToPath(componentsUrl), { withFileTypes: true })
    .filter(
      (entry) => entry.isFile() && entry.name.endsWith(".tsx") && !entry.name.includes(".test."),
    )
    .map((entry) => `${feature}/components/${entry.name}`);
}

/** Every non-spec `.tsx` under `src/features`, as `<feature>/components/<File>.tsx`. */
function featureSources(): string[] {
  return readdirSync(fileURLToPath(featuresDir), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((feature) => componentsOf(feature.name))
    .toSorted();
}

const ALL_SOURCES: readonly string[] = featureSources();

function source(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, featuresDir)), "utf8");
}

/**
 * The six components that render a card-wrapped list. Every one of them owns
 * both halves — the surface AND the rows — so each must reach for both catalog
 * components.
 *
 * `MemberList`, `OrganizationDetail` and `InquiryDetail` are here even though
 * the bead text names only the first three: their lists live in components
 * nested inside those files (`MemberTable`/`MemberRow`, `ClientsTable`/
 * `ClientRow`, `CommentThread`/`CommentRow`) rather than at the top level, which
 * is exactly the shape this epic's inventories keep missing.
 */
const LIST_FILES: readonly string[] = [
  "apps/components/AppList.tsx",
  "inquiries/components/InquiryDetail.tsx",
  "inquiries/components/InquiryList.tsx",
  "organizations/components/MemberList.tsx",
  "organizations/components/OrganizationDetail.tsx",
  "organizations/components/OrganizationList.tsx",
];

/**
 * The components with a nothing-to-show branch that owns a surface. Four are the
 * lists' empty states; `OrganizationDetail`'s is its not-found card, which is
 * the SAME hand-rolled centered card down to the class string and so cannot be
 * spared without carving an arbitrary hole in the sweep below.
 *
 * Two nothing-to-show branches are deliberately absent, both because they render
 * no surface of their own: `InquiryDetail`'s `inquiry-comments-empty` and its
 * `inquiry-detail-not-found` are bare sentences inside the detail card, which
 * makes them a `Text` question (F5.T3) rather than an `EmptyState` one.
 */
const EMPTY_STATE_FILES: readonly string[] = [
  "apps/components/AppList.tsx",
  "inquiries/components/InquiryList.tsx",
  "organizations/components/MemberList.tsx",
  "organizations/components/OrganizationDetail.tsx",
  "organizations/components/OrganizationList.tsx",
];

/** The six components that render a status, type or role chip. */
const BADGE_FILES: readonly string[] = [
  "apps/components/AppList.tsx",
  "inquiries/components/InquiryDetail.tsx",
  "inquiries/components/InquiryList.tsx",
  "mfa/components/MfaSettingsSection.tsx",
  "organizations/components/OrganizationList.tsx",
  "settings/components/ProfileSection.tsx",
];

/**
 * The two files allowed to keep a raw `<ul>`/`<li>`: both reveal one-time backup
 * codes as a plain content list inside a framed panel. Neither is a list CARD —
 * there is no surface, no row cell and no per-row test id — so `ListCard` would
 * paint a card around a paragraph's worth of codes.
 */
const RAW_LIST_EXEMPT: ReadonlySet<string> = new Set([
  "mfa/components/MfaEnrollFlow.tsx",
  "mfa/components/MfaSettingsSection.tsx",
]);

function importsFromUi(symbol: string): RegExp {
  return new RegExp(
    String.raw`import\s*\{[^}]*\b${symbol}\b[^}]*\}\s*from\s*"@bc-solutions-coder/ui"`,
    "u",
  );
}

/** The list-card surface string every card-wrapped list used to hand-roll. */
const HAND_ROLLED_LIST_SURFACE =
  "bg-card rounded-lg shadow-sm border border-border overflow-hidden";

/** The row cell string, in both spellings the app shipped (inline and as a const). */
const HAND_ROLLED_ROW_CELL = "flex items-center justify-between px-6 py-4";

/** The centered card the emoji empty states hand-rolled, at either padding. */
const HAND_ROLLED_EMPTY_CARD =
  /bg-card rounded-lg shadow-sm border border-border p-\d+ text-center/u;

/** The chip class string six surfaces duplicated, inline or behind a `CHIP` const. */
const HAND_ROLLED_CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

describe("wallow-web lists render through the catalog", () => {
  it("finds every feature component on disk", () => {
    // Guards the sweep itself: an empty enumeration would make every "no feature
    // file contains X" assertion below pass vacuously.
    expect(ALL_SOURCES.length).toBeGreaterThanOrEqual(LIST_FILES.length);
    expect(ALL_SOURCES).toEqual(expect.arrayContaining([...LIST_FILES]));
  });

  it.each(LIST_FILES)("composes the catalog list card in %s", (file) => {
    expect(source(file)).toMatch(importsFromUi("ListCard"));
  });

  it.each(LIST_FILES)("composes the catalog list row in %s", (file) => {
    expect(source(file)).toMatch(importsFromUi("ListRow"));
  });

  it.each(LIST_FILES)("hand-rolls no list element in %s", (file) => {
    const text = source(file);

    expect(text, "the <ul> is ListCard's").not.toContain("<ul");
    expect(text, "the <li> is ListRow's").not.toContain("<li");
  });

  it.each(ALL_SOURCES)("hand-rolls no list-card surface in %s", (file) => {
    expect(source(file), "the surface is listCardRecipe()'s").not.toContain(
      HAND_ROLLED_LIST_SURFACE,
    );
  });

  it.each(ALL_SOURCES)("hand-rolls no list-row cell in %s", (file) => {
    expect(source(file), "the row cell is listRowRecipe()'s").not.toContain(HAND_ROLLED_ROW_CELL);
  });

  it("leaves a raw list only where the codes reveal needs one", () => {
    const offenders = ALL_SOURCES.filter(
      (file) => !RAW_LIST_EXEMPT.has(file) && source(file).includes("<ul"),
    );

    expect(offenders, "a card-wrapped list is a ListCard").toEqual([]);
  });
});

describe("wallow-web empty states render through the catalog", () => {
  it.each(EMPTY_STATE_FILES)("composes the catalog empty state in %s", (file) => {
    expect(source(file)).toMatch(importsFromUi("EmptyState"));
  });

  it.each(ALL_SOURCES)("hand-rolls no centered empty card in %s", (file) => {
    expect(source(file), "the empty card is EmptyState's").not.toMatch(HAND_ROLLED_EMPTY_CARD);
  });
});

describe("wallow-web status chips render through the catalog", () => {
  it.each(BADGE_FILES)("composes the catalog badge in %s", (file) => {
    expect(source(file)).toMatch(importsFromUi("Badge"));
  });

  it.each(ALL_SOURCES)("hand-rolls no chip class string in %s", (file) => {
    expect(source(file), "the chip is badgeRecipe()'s").not.toContain(HAND_ROLLED_CHIP);
  });

  it("leaves no CHIP constant behind", () => {
    const offenders = ALL_SOURCES.filter((file) => /^const CHIP\b/mu.test(source(file)));

    expect(offenders, "a chip constant is a Badge that was not migrated").toEqual([]);
  });
});
