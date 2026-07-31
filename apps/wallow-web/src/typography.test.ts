import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The typography + colour-alpha sweep (Wallow-lrlm.5.3) — the broadest of the
 * F5 migrations, and the one with the widest blast radius, so it is guarded at
 * the SOURCE rather than component by component.
 *
 * Two contracts, both stated negatively:
 *
 *   1. **No app-authored text element.** `packages/ui`'s `Text` owns the type
 *      scale and the semantic colour of every piece of body and heading copy.
 *      A raw `<p>`/`<span>`/`<h1>`..`<h6>`/`<legend>`/`<code>` in this app is a
 *      second place the scale gets decided, which is exactly what F5 removes.
 *   2. **No alpha-modified colour utility.** `text-foreground/60` is a colour
 *      this theme cannot name — a fork editing `branding.json` cannot reach it,
 *      and two files reaching for the same `/70` have agreed on a design
 *      meaning without ever writing it down. Muted body copy is
 *      `text-muted-foreground` (`Text color="muted"`); a recessed surface is
 *      `bg-muted` (the decision Wallow-lrlm.3.5 already made for `ListRow`).
 *
 * The sweep is DISK-DERIVED, not a hand-kept list: this epic has twice shipped
 * a component that no inventory mentioned, so every `.tsx` under
 * `features/*\/components` and `shared/components` is judged whether or not
 * anyone remembered it. `KNOWN_COMPONENTS` exists only to prove the walk is not
 * vacuous.
 *
 * Both sweeps read the file with its COMMENTS STRIPPED. The guards F5.T1 added
 * cost two edit passes to prose that merely mentioned `<h1>`, and a doc comment
 * describing the markup a component no longer hand-rolls is worth keeping.
 */

const srcDir: URL = new URL("./", import.meta.url);

function read(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, srcDir)), "utf8");
}

/** Every non-test `.tsx` directly inside `directory`, as a src-relative path. */
function componentsIn(directory: string): string[] {
  return readdirSync(fileURLToPath(new URL(directory, srcDir)), { withFileTypes: true })
    .filter(
      (entry) => entry.isFile() && entry.name.endsWith(".tsx") && !entry.name.includes(".test."),
    )
    .map((entry) => `${directory}${entry.name}`);
}

/** Every feature directory under `features/`, in directory order. */
function featureNames(): string[] {
  return readdirSync(fileURLToPath(new URL("features/", srcDir)), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
}

/**
 * Every component this bead sweeps: the feature components plus the shared
 * ones, which is precisely the surface Wallow-lrlm.5.3's acceptance criteria
 * name (`src/features` and `src/shared/components`).
 */
function sweptComponents(): string[] {
  return [
    ...featureNames().flatMap((feature) => componentsIn(`features/${feature}/components/`)),
    ...componentsIn("shared/components/"),
  ];
}

const SWEPT: readonly string[] = sweptComponents();

/**
 * The components on disk when this bead was written. The walk above is what
 * actually judges the app; this list only fails when the walk stops finding
 * files it used to find, so a broken glob shows up as a red spec rather than as
 * a sweep that silently passes over nothing.
 */
const KNOWN_COMPONENTS: readonly string[] = [
  "features/apps/components/AppList.tsx",
  "features/apps/components/RegisterAppForm.tsx",
  "features/inquiries/components/CreateInquiryForm.tsx",
  "features/inquiries/components/InquiryDetail.tsx",
  "features/inquiries/components/InquiryList.tsx",
  "features/landing/components/LandingPage.tsx",
  "features/mfa/components/MfaEnrollFlow.tsx",
  "features/mfa/components/MfaSettingsSection.tsx",
  "features/organizations/components/CreateOrganizationForm.tsx",
  "features/organizations/components/MemberList.tsx",
  "features/organizations/components/OrganizationDetail.tsx",
  "features/organizations/components/OrganizationList.tsx",
  "features/settings/components/ProfileSection.tsx",
  "shared/components/DashboardLayout.tsx",
  "shared/components/DashboardNav.tsx",
  "shared/components/PublicLayout.tsx",
  "shared/components/SelectControl.tsx",
  "shared/components/ready-indicator.tsx",
];

/**
 * The files hand-rolling a text element before this bead. They are the ones
 * that must end up importing a `Text` primitive, and listing them keeps the
 * import assertion from passing on a file that renders no copy at all.
 */
const HAND_ROLLED_TEXT: readonly string[] = [
  "features/apps/components/AppList.tsx",
  "features/apps/components/RegisterAppForm.tsx",
  "features/inquiries/components/CreateInquiryForm.tsx",
  "features/inquiries/components/InquiryDetail.tsx",
  "features/inquiries/components/InquiryList.tsx",
  "features/landing/components/LandingPage.tsx",
  "features/mfa/components/MfaEnrollFlow.tsx",
  "features/mfa/components/MfaSettingsSection.tsx",
  "features/organizations/components/MemberList.tsx",
  "features/organizations/components/OrganizationDetail.tsx",
  "features/organizations/components/OrganizationList.tsx",
  "features/settings/components/ProfileSection.tsx",
  "shared/components/PublicLayout.tsx",
];

/**
 * The dashboard shell's inverted chrome is Wallow-lrlm.5.4's, whose own
 * acceptance criteria name these two files and their sidebar alpha modifiers.
 * They are exempt HERE so the two beads do not both claim the same edit — not
 * because the alpha is allowed to survive the epic.
 */
const SIDEBAR_INVERSION: ReadonlySet<string> = new Set([
  "shared/components/DashboardLayout.tsx",
  "shared/components/DashboardNav.tsx",
]);

/**
 * Strip block and line comments. Crude by design — it also truncates a `//`
 * inside a string literal, which no assertion below depends on.
 */
function code(relativePath: string): string {
  return read(relativePath)
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/\/\/[^\n]*/gu, "");
}

/** A JSX opener for `tag`, however the element wraps across lines. */
function opener(tag: string): RegExp {
  return new RegExp(String.raw`<${tag}(?=[\s/>\n])`, "u");
}

/** The elements whose job `Text` now does. Each has a `Text` `as` value. */
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

/**
 * A theme colour utility carrying an opacity modifier, e.g.
 * `text-foreground/60`, `hover:bg-background/50`, `border-foreground/20`.
 */
const ALPHA_COLOR =
  /\b[a-z-]*(?:text|bg|border|ring|divide|fill|stroke)-(?:foreground|background|card|card-foreground|primary|primary-foreground|secondary|muted|muted-foreground|accent|accent-foreground|destructive|border|ring|sidebar|sidebar-foreground|sidebar-accent|success)\/\d{1,3}\b/gu;

/** `symbol` is imported from the component catalog. */
function importsFromUi(symbol: string): RegExp {
  return new RegExp(
    String.raw`import\s*(?:type\s*)?\{[^}]*\b${symbol}\b[^}]*\}\s*from\s*"@bc-solutions-coder/ui"`,
    "u",
  );
}

describe("wallow-web renders its copy through the catalog's Text", () => {
  it("sweeps every component on disk", () => {
    expect(SWEPT).toEqual(expect.arrayContaining([...KNOWN_COMPONENTS]));
    expect(SWEPT.length).toBeGreaterThanOrEqual(KNOWN_COMPONENTS.length);
  });

  it.each(SWEPT)("hand-rolls no text element in %s", (file) => {
    const source = code(file);
    const found = TEXT_ELEMENTS.filter((tag) => opener(tag).test(source));

    expect(found, `${file} must render its copy through Text, not raw elements`).toEqual([]);
  });

  it.each(HAND_ROLLED_TEXT)("imports a Text primitive in %s", (file) => {
    const source = read(file);
    const imported = importsFromUi("Text").test(source) || importsFromUi("MutedText").test(source);

    expect(imported, `${file} renders copy, so it must import Text (or MutedText)`).toBe(true);
  });

  /*
   * `Text` is polymorphic but its DEFAULT variant follows the element: `as="h2"`
   * derives `title` (`text-3xl`), which is not the `text-xl font-semibold` these
   * section headings ship today. Every migrated `h2` therefore has to name
   * `variant="subheading"` rather than lean on the default, or the restyle
   * silently doubles the section-heading scale.
   */
  it.each([
    "features/inquiries/components/CreateInquiryForm.tsx",
    "features/organizations/components/MemberList.tsx",
    "features/organizations/components/OrganizationDetail.tsx",
  ])("keeps its section headings at the subheading scale in %s", (file) => {
    const named = code(file).includes('variant="subheading"');

    expect(named, `${file}'s section heading must not take Text's h2 default`).toBe(true);
  });

  /*
   * Wallow-lrlm.5.1 deferred these two page titles to this bead: they are the
   * detail routes' `<h1>`s, which live in the COMPONENT rather than in the route
   * file the page-shell sweep covered. Their shipped test ids are pinned by
   * `*.restyle.test.tsx` and by the E2E page objects, so the id has to stay on
   * the heading element itself.
   */
  it.each([
    ["features/inquiries/components/InquiryDetail.tsx", "inquiry-detail-heading"],
    ["features/organizations/components/OrganizationDetail.tsx", "organization-detail-heading"],
  ])("renders %s's page title through Text, keeping %s", (file, testId) => {
    const source = code(file);

    expect(opener("h1").test(source), `${file} must not hand-roll the page title`).toBe(false);
    expect(source, `${testId} must stay on the heading element`).toContain(
      `data-testid="${testId}"`,
    );
  });

  /*
   * Body copy wrapped in a bare `<div>` is the same decision as a bare `<span>`,
   * so the sweep would miss it: `div` is a layout element this app still needs.
   * These three are the text-carrying divs on disk, named individually.
   */
  it.each([
    ["features/inquiries/components/InquiryDetail.tsx", "inquiry-detail-email"],
    ["features/settings/components/ProfileSection.tsx", "settings-profile-name"],
    ["features/settings/components/ProfileSection.tsx", "settings-profile-email"],
  ])("does not wrap %s's %s in a bare div", (file, testId) => {
    const wrapper = new RegExp(String.raw`<div[^>]*data-testid="${testId}"`, "u");

    expect(wrapper.test(code(file)), `${testId} is copy, so it belongs in Text`).toBe(false);
  });

  /*
   * The same read-only field caption and value strings are declared in BOTH the
   * settings profile card and the MFA card. `Text`'s `overline` variant is
   * `text-xs font-semibold uppercase tracking-wider` and `bodySm` is `text-sm`,
   * so after the migration neither file has a type-scale string left to keep in
   * sync with the other.
   */
  it.each([
    "features/mfa/components/MfaSettingsSection.tsx",
    "features/settings/components/ProfileSection.tsx",
  ])("declares no hand-rolled field type scale in %s", (file) => {
    const source = code(file);
    const leftovers = ["uppercase tracking-wider", "text-sm text-foreground"].filter((scale) =>
      source.includes(scale),
    );

    expect(leftovers, `${file} must take these scales from Text's variants`).toEqual([]);
  });
});

describe("wallow-web names its colours instead of tinting them", () => {
  it.each(SWEPT.filter((file) => !SIDEBAR_INVERSION.has(file)))(
    "uses no alpha-modified colour utility in %s",
    (file) => {
      const found = [...new Set(code(file).match(ALPHA_COLOR))];

      expect(found, `${file} must name these colours as tokens, not tint them`).toEqual([]);
    },
  );

  /*
   * The one alpha modifier in this bead's scope that paints a SURFACE rather
   * than text. Wallow-lrlm.3.5 already ruled on this exact utility when
   * `ListRow`'s `hover:bg-background/50` became `hover:bg-muted`, so the confirm
   * panel follows the catalog rather than inventing a second answer.
   */
  it("recesses the MFA confirm panel with the muted surface token", () => {
    const recessed = code("features/mfa/components/MfaSettingsSection.tsx").includes("bg-muted");

    expect(recessed, "the confirm panel's recessed surface is `bg-muted`").toBe(true);
  });
});
