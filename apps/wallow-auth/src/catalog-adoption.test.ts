import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * wallow-auth's catalog adoption (Wallow-lrlm.7.1) — the source half of bringing
 * this app up to the level wallow-web reached in F5. The sibling of
 * `apps/wallow-web/src/typography.test.ts`, asserting the same contracts on the
 * other origin, plus the button half F5 folded into its lint gate.
 *
 * Three contracts, all stated against the SOURCE because they are claims about
 * the source — "no hand-rolled copy is left behind somewhere the tests happen
 * not to look" is not something a render can say:
 *
 *   1. **No app-authored text element.** `packages/ui`'s `Text` owns the type
 *      scale and the semantic colour of every piece of body and heading copy. A
 *      raw `<p>`/`<span>`/`<h1>`..`<h6>`/`<legend>`/`<code>` here is a second
 *      place the scale gets decided.
 *   2. **No app-authored button.** The upgraded `Button` recipe (F3.T1) covers
 *      solid, outline, ghost and link treatments across four size steps, so a
 *      `<button className="rounded-md bg-primary px-3 py-2 …">` in this app is a
 *      copy of it that drifts on its own.
 *   3. **Every heading through `Text` names its variant.** `Text` derives the
 *      scale from `as` when the caller supplies none, and those defaults are
 *      much larger than what these cards ship: `as="h2"` derives `title`
 *      (`text-3xl`) and `as="h3"` derives `heading` (`text-2xl`). A migration
 *      that leans on the default silently triples a card heading.
 *
 * WHAT THIS FILE DOES NOT CLAIM. Whether a control actually PAINTS the variant
 * it names is invisible to a class-string read — the real class list is the
 * `twMerge` of the recipe with a caller `className`, so a spec can pin the
 * recipe, stay green, and ship the wrong box. That half is measured in a real
 * browser by `features/consent/components/ConsentScreen.catalog.test.tsx` and
 * `features/mfa-challenge/components/MfaChallengeForm.catalog.test.tsx`.
 *
 * The sweep is DISK-DERIVED, not a hand-kept list: the inventories in this
 * epic's bead bodies have repeatedly been incomplete, so every `.tsx` under
 * `features/*\/components`, `shared/components` and `app/routes` is judged
 * whether or not anyone remembered it. `KNOWN_COMPONENTS` exists only to prove
 * the walk is not vacuous.
 *
 * Runs on the NODE project (`*.test.ts`) — it reads files and renders nothing.
 */

const srcDir: URL = new URL("./", import.meta.url);

function read(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, srcDir)), "utf8");
}

/**
 * Strip block and line comments in ONE left-to-right pass.
 *
 * THE ORDER IS THE WHOLE POINT, and it is why this is not a copy of
 * wallow-web's two-step `replaceAll(block).replaceAll(line)`. wallow-auth's
 * source is full of line comments naming proxy globs — `// … \`/v1/**\` is
 * served by the passthrough …` in `AcceptTermsScreen.tsx` is the one that bites.
 * Removing block comments FIRST sees the `/*` inside that glob, opens a comment
 * there, and runs to the file's next `*\/` — forty lines later, taking the
 * screen's heading and its submit button with it. Every assertion below then
 * passes over source that was silently deleted.
 *
 * A single alternation cannot do that: at the `//` the line arm matches first
 * (it starts earlier), so the glob is consumed as part of the line comment.
 * Still crude by design — it also truncates a `//` inside a string literal,
 * which no assertion below depends on.
 */
function stripComments(source: string): string {
  return source.replaceAll(/\/\*[\s\S]*?\*\/|\/\/[^\n]*/gu, "");
}

/** A file's source with its comments stripped. */
function code(relativePath: string): string {
  return stripComments(read(relativePath));
}

/** Directories a source scan should never have to descend into. */
const IGNORED_DIRS: ReadonlySet<string> = new Set([
  "node_modules",
  "dist",
  ".vite",
  ".output",
  "__screenshots__",
]);

/** Every non-test `.tsx` directly inside `directory`, as a src-relative path. */
function componentsIn(directory: string): string[] {
  return readdirSync(fileURLToPath(new URL(directory, srcDir)), { withFileTypes: true })
    .filter(
      (entry) => entry.isFile() && entry.name.endsWith(".tsx") && !entry.name.includes(".test."),
    )
    .map((entry) => `${directory}${entry.name}`);
}

/** Every non-test `.tsx` under `directory`, recursively. Routes nest. */
function componentsUnder(directory: string): string[] {
  return readdirSync(fileURLToPath(new URL(directory, srcDir)), { withFileTypes: true }).flatMap(
    (entry) => {
      if (entry.isDirectory()) {
        return IGNORED_DIRS.has(entry.name) ? [] : componentsUnder(`${directory}${entry.name}/`);
      }

      return entry.isFile() && entry.name.endsWith(".tsx") && !entry.name.includes(".test.")
        ? [`${directory}${entry.name}`]
        : [];
    },
  );
}

/** Every feature directory under `features/`, in directory order. */
function featureNames(): string[] {
  return readdirSync(fileURLToPath(new URL("features/", srcDir)), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name);
}

/**
 * Every component this bead sweeps: the feature components, the shared ones, and
 * the route components. The routes carry no copy today — including them is what
 * keeps a migrated screen from pushing its markup up into its route file.
 */
function sweptComponents(): string[] {
  return [
    ...featureNames().flatMap((feature) => componentsIn(`features/${feature}/components/`)),
    ...componentsIn("shared/components/"),
    ...componentsUnder("app/routes/"),
  ];
}

const SWEPT: readonly string[] = sweptComponents();

/**
 * The feature and shared components on disk when this bead was written. The walk
 * above is what actually judges the app; this list only fails when the walk
 * stops finding files it used to find, so a broken glob shows up as a red spec
 * rather than as a sweep that silently passes over nothing.
 */
const KNOWN_COMPONENTS: readonly string[] = [
  "features/accept-terms/components/AcceptTermsScreen.tsx",
  "features/consent/components/ConsentScreen.tsx",
  "features/error/components/ErrorPage.tsx",
  "features/forgot-password/components/ForgotPasswordForm.tsx",
  "features/invitation/components/InvitationScreen.tsx",
  "features/login/components/ExternalProviders.tsx",
  "features/login/components/LoginScreen.tsx",
  "features/login/components/MagicLinkLoginForm.tsx",
  "features/login/components/OtpLoginForm.tsx",
  "features/login/components/PasswordLoginForm.tsx",
  "features/logout/components/LogoutScreen.tsx",
  "features/mfa-challenge/components/MfaChallengeForm.tsx",
  "features/mfa-enroll/components/MfaEnrollForm.tsx",
  "features/not-found/components/NotFoundPage.tsx",
  "features/privacy/components/PrivacyPage.tsx",
  "features/register/components/RegisterForm.tsx",
  "features/reset-password/components/ResetPasswordForm.tsx",
  "features/terms/components/TermsPage.tsx",
  "features/verify-email/components/VerifyEmailConfirm.tsx",
  "features/verify-email/components/VerifyEmailNotice.tsx",
  "shared/components/auth-layout.tsx",
  "shared/components/ready-indicator.tsx",
];

/**
 * The files hand-rolling a text element before this bead — re-derived from disk
 * with the stripper above rather than taken from the bead body, which named
 * "~20 files with raw `<h1..h6>`" when only eight of them carry a heading at
 * all. They are the ones that must end up importing a `Text` primitive, and
 * listing them keeps the import assertion from passing on a file that renders no
 * copy at all.
 */
const HAND_ROLLED_TEXT: readonly string[] = [
  "features/accept-terms/components/AcceptTermsScreen.tsx",
  "features/consent/components/ConsentScreen.tsx",
  "features/error/components/ErrorPage.tsx",
  "features/forgot-password/components/ForgotPasswordForm.tsx",
  "features/invitation/components/InvitationScreen.tsx",
  "features/login/components/ExternalProviders.tsx",
  "features/login/components/LoginScreen.tsx",
  "features/login/components/MagicLinkLoginForm.tsx",
  "features/logout/components/LogoutScreen.tsx",
  "features/mfa-challenge/components/MfaChallengeForm.tsx",
  "features/mfa-enroll/components/MfaEnrollForm.tsx",
  "features/privacy/components/PrivacyPage.tsx",
  "features/register/components/RegisterForm.tsx",
  "features/reset-password/components/ResetPasswordForm.tsx",
  "features/terms/components/TermsPage.tsx",
  "features/verify-email/components/VerifyEmailConfirm.tsx",
  "features/verify-email/components/VerifyEmailNotice.tsx",
  "shared/components/auth-layout.tsx",
];

/** The files that render a heading, and so must name a `Text` variant. */
const HEADING_FILES: readonly string[] = [
  "features/accept-terms/components/AcceptTermsScreen.tsx",
  "features/consent/components/ConsentScreen.tsx",
  "features/error/components/ErrorPage.tsx",
  "features/forgot-password/components/ForgotPasswordForm.tsx",
  "features/invitation/components/InvitationScreen.tsx",
  "features/login/components/LoginScreen.tsx",
  "features/logout/components/LogoutScreen.tsx",
  "features/mfa-challenge/components/MfaChallengeForm.tsx",
  "features/mfa-enroll/components/MfaEnrollForm.tsx",
  "features/not-found/components/NotFoundPage.tsx",
  "features/privacy/components/PrivacyPage.tsx",
  "features/register/components/RegisterForm.tsx",
  "features/reset-password/components/ResetPasswordForm.tsx",
  "features/terms/components/TermsPage.tsx",
  "features/verify-email/components/VerifyEmailConfirm.tsx",
  "features/verify-email/components/VerifyEmailNotice.tsx",
  "shared/components/auth-layout.tsx",
];

/**
 * The sixteen screens carrying a CARD heading — every `HEADING_FILES` entry bar
 * `auth-layout.tsx`, which owns the page's `<h1>` and is not a card heading at
 * all. Listed only to prove the disk-derived walk below is not vacuous; the walk
 * is what actually judges the app.
 */
const KNOWN_CARD_HEADINGS: readonly string[] = HEADING_FILES.filter(
  (file) => file !== "shared/components/auth-layout.tsx",
);

/** The files hand-rolling a `<button>` before this bead. */
const HAND_ROLLED_BUTTONS: readonly string[] = [
  "features/consent/components/ConsentScreen.tsx",
  "features/invitation/components/InvitationScreen.tsx",
  "features/mfa-challenge/components/MfaChallengeForm.tsx",
];

/**
 * The class strings those files spell out today, each one a fragment of the
 * `Button` recipe copied by hand. The `<button>` sweep alone would not catch a
 * migration that moved the same string onto a `<Button className=…>`.
 */
const BUTTON_RECIPE_COPIES: ReadonlyArray<readonly [string, string]> = [
  ["features/consent/components/ConsentScreen.tsx", "bg-primary px-3 py-2 text-sm font-medium"],
  [
    "features/consent/components/ConsentScreen.tsx",
    "border border-border px-3 py-2 text-sm font-medium",
  ],
  ["features/invitation/components/InvitationScreen.tsx", "rounded-md bg-primary px-3 py-2"],
  [
    "features/invitation/components/InvitationScreen.tsx",
    "rounded-md border border-border px-3 py-2",
  ],
];

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

/** `symbol` is imported from the component catalog. */
function importsFromUi(symbol: string): RegExp {
  return new RegExp(
    String.raw`import\s*(?:type\s*)?\{[^}]*\b${symbol}\b[^}]*\}\s*from\s*"@bc-solutions-coder/ui"`,
    "u",
  );
}

/** Every `<Text …>` opener in `source`, as its raw attribute list. */
function textOpeners(source: string): string[] {
  return [...source.matchAll(/<Text\b([^>]*)>/gu)].map((match) => match[1] ?? "");
}

/**
 * Every `<Text as="h2" …>` opener in `source` — this app's CARD headings.
 *
 * Level 2 is the card-heading level throughout wallow-auth: `AuthLayout` owns
 * the page's one `<h1>`, and the privacy/terms document sections are `<h3>`s at
 * the `bodySm` step. So the level is the selector, and no screen can duck this
 * sweep by omitting a testid.
 */
function cardHeadingOpeners(source: string): string[] {
  return textOpeners(source).filter((attrs) => /\bas="h2"/u.test(attrs));
}

/** The swept components that render a card heading, re-derived from disk. */
const CARD_HEADING_FILES: readonly string[] = SWEPT.filter(
  (file) => cardHeadingOpeners(code(file)).length > 0,
);

describe("the comment stripper this sweep reads through", () => {
  /*
   * The guard on the guard. Every assertion in this file reads `code(file)`, so
   * a stripper that eats real source turns the whole sweep green over nothing —
   * and wallow-auth's source contains the exact input that does it.
   */
  it("does not let a glob inside a line comment open a block comment", () => {
    const source: string = [
      "// `/v1/**` is served by the passthrough reverse proxy",
      '<h1 data-testid="probe">Almost there!</h1>',
      "/* a genuine block comment */",
      "const kept = 1;",
    ].join("\n");

    const stripped: string = stripComments(source);

    expect(stripped, "the heading after the glob survives").toContain('data-testid="probe"');
    expect(stripped, "the trailing code survives").toContain("const kept = 1;");
    expect(stripped, "the block comment still goes").not.toContain("a genuine block comment");
    expect(stripped, "the line comment still goes").not.toContain("served by the passthrough");
  });

  it("keeps the code a real file's glob comment would otherwise swallow", () => {
    // AcceptTermsScreen's hand-off comment names `/v1/**`, and the file's next
    // `*/` is a JSX comment forty lines below it. Under a block-comments-first
    // stripper everything between them disappears — including this testid and
    // the heading beside it.
    const source: string = code("features/accept-terms/components/AcceptTermsScreen.tsx");

    expect(source).toContain('data-testid="accept-terms-submit"');
  });
});

describe("wallow-auth renders its copy through the catalog's Text", () => {
  it("sweeps every component on disk", () => {
    expect(SWEPT).toEqual(expect.arrayContaining([...KNOWN_COMPONENTS]));
    expect(SWEPT.length).toBeGreaterThanOrEqual(KNOWN_COMPONENTS.length);
    // The route half of the walk, which `KNOWN_COMPONENTS` deliberately does not
    // enumerate: routes churn, but the walk reaching them at all is load-bearing.
    expect(SWEPT).toContain("app/routes/__root.tsx");
  });

  it.each(SWEPT)("hand-rolls no text element in %s", (file) => {
    const source: string = code(file);
    const found: string[] = TEXT_ELEMENTS.filter((tag) => opener(tag).test(source));

    expect(found, `${file} must render its copy through Text, not raw elements`).toEqual([]);
  });

  it.each(HAND_ROLLED_TEXT)("imports a Text primitive in %s", (file) => {
    const source: string = read(file);
    const imported: boolean =
      importsFromUi("Text").test(source) || importsFromUi("MutedText").test(source);

    expect(imported, `${file} renders copy, so it must import Text (or MutedText)`).toBe(true);
  });

  /*
   * `Text` is polymorphic but its DEFAULT variant follows the element, and those
   * defaults are far larger than what these cards ship: `as="h2"` derives
   * `title` (`text-3xl`) against the `text-lg` these headings wear today, and
   * the privacy/terms `<h3>`s carry no size class at all. Every migrated heading
   * therefore has to NAME its variant rather than lean on the default.
   */
  it.each(HEADING_FILES)("names a variant on every heading Text in %s", (file) => {
    const headings: string[] = textOpeners(code(file)).filter((attrs) =>
      /\bas="h[1-6]"/u.test(attrs),
    );

    expect(headings.length, `${file} must render its heading through Text`).toBeGreaterThan(0);
    expect(
      headings.filter((attrs) => !/\bvariant="/u.test(attrs)),
      `${file}'s headings must not take Text's as-derived default scale`,
    ).toEqual([]);
  });

  /*
   * `Text` has no `text-lg` step — the scale runs …`heading` (2xl), `subheading`
   * (xl), `body` (base)… — so a migration that kept these cards at `text-lg`
   * could only do it by writing the size back as a `className`, which is the
   * second place the scale gets decided that this bead removes.
   */
  it.each(SWEPT)("declares no hand-rolled heading scale in %s", (file) => {
    expect(code(file), `${file} must take its heading scale from a Text variant`).not.toContain(
      "text-lg font-semibold",
    );
  });

  /*
   * `AuthLayout` owns the page's ONE `<h1>`, and it is `FocusOnNavigate`'s
   * route-change focus target. Two screens open a SECOND `<h1>` inside their
   * card today — an accessibility defect the migration must not carry across
   * verbatim, since `Text` makes the level an explicit argument.
   */
  it.each([
    "features/accept-terms/components/AcceptTermsScreen.tsx",
    "features/logout/components/LogoutScreen.tsx",
  ])("opens no second level-1 heading in %s", (file) => {
    const headings: string[] = textOpeners(code(file)).filter((attrs) =>
      /\bas="h[1-6]"/u.test(attrs),
    );

    // The precondition, not decoration: a file that renders no heading through
    // `Text` at all would satisfy "no `as=\"h1\"`" without having migrated.
    expect(headings.length, `${file} must render its card heading through Text`).toBeGreaterThan(0);
    expect(
      headings.filter((attrs) => /\bas="h1"/u.test(attrs)),
      `${file}'s card heading sits under AuthLayout's <h1>`,
    ).toEqual([]);
  });

  it("keeps the branded page title as the app's only level-1 heading", () => {
    const source: string = code("shared/components/auth-layout.tsx");
    const levelOne: string[] = textOpeners(source).filter((attrs) => /\bas="h1"/u.test(attrs));

    expect(levelOne, "the branded name is the page title").toHaveLength(1);
    // The focus seam has to survive the migration: `Text` spreads its rest props
    // onto the element, so both attributes belong on the `Text` itself.
    expect(levelOne[0], "FocusOnNavigate's target attribute").toContain("data-focus-target");
    expect(levelOne[0], "the programmatic focus target stays unreachable by Tab").toContain(
      "tabIndex={-1}",
    );
  });
});

/**
 * The card-heading scale (Wallow-lrlm.13) — the SOURCE half of the pin.
 *
 * THE DEFECT. wallow-auth's sixteen screen headings used to render at two sizes
 * on the same card slot: nine at `Text`'s `subheading` step (`text-xl`, 20px)
 * and seven at `CardTitle`'s own `text-lg` literal (18px). The standard is now
 * `Text`'s `body` step at `semibold` — 16px — composed identically everywhere:
 * `<Text as="h2" variant="body" weight="semibold" color="onCard">`.
 *
 * WHY THE SOURCE, GIVEN `src/heading-scale.test.tsx` MEASURES IT. That file
 * mounts all sixteen screens and proves they PAINT one size. What it cannot
 * prove is anything about a SEVENTEENTH — a screen it was never told to mount is
 * exactly what a render cannot see, and "no screen was forgotten" is a claim
 * about the tree, not about a layout. This walk is disk-derived for that reason:
 * the inventories in this epic's bead bodies have repeatedly been incomplete, so
 * the next screen is judged the day it lands rather than the day someone
 * remembers to add it to a list.
 *
 * WHY NOT `CardTitle`. Landing these sixteen on 16px through the catalog part
 * would mean retuning `cardTitleRecipe` in `packages/ui`, which also moves
 * wallow-web's six card headings and minimal-app's two. That is a separate,
 * cross-app decision, so wallow-auth composes `Text` directly instead and the
 * shared recipe is left alone.
 */
describe("wallow-auth renders every screen heading at one scale", () => {
  it("finds a card heading on every screen known to carry one", () => {
    // The vacuity guard on the walk: this only fails when the sweep stops
    // finding headings it used to find, so a heading that moved out of reach
    // shows up as a red spec rather than as a walk over nothing.
    expect(CARD_HEADING_FILES).toEqual(expect.arrayContaining([...KNOWN_CARD_HEADINGS]));
    expect(CARD_HEADING_FILES.length).toBeGreaterThanOrEqual(KNOWN_CARD_HEADINGS.length);
  });

  it.each(CARD_HEADING_FILES)("puts %s's card heading on the body step", (file) => {
    const headings: string[] = cardHeadingOpeners(code(file));

    expect(
      headings.filter((attrs) => !/\bvariant="body"/u.test(attrs)),
      `${file}'s card heading must take the app-wide card-heading scale`,
    ).toEqual([]);
  });

  it.each(CARD_HEADING_FILES)("keeps %s's card heading at semibold", (file) => {
    // `body` carries no `font-*` of its own, so the weight is a second explicit
    // argument rather than something the scale brings along: drop it and the
    // heading reads at the same size as the copy underneath it.
    const headings: string[] = cardHeadingOpeners(code(file));

    expect(
      headings.filter((attrs) => !/\bweight="semibold"/u.test(attrs)),
      `${file}'s card heading must stay distinguishable from body copy`,
    ).toEqual([]);
  });

  it.each(SWEPT)("takes no card heading from CardTitle in %s", (file) => {
    // `CardTitle` hard-codes `text-lg`, which is one of the two steps this app
    // just left. Re-importing it anywhere here re-opens the split.
    expect(code(file), `${file} must compose its heading from Text`).not.toContain("CardTitle");
  });
});

describe("wallow-auth builds its controls from the catalog's Button", () => {
  it.each(SWEPT)("hand-rolls no button element in %s", (file) => {
    expect(
      opener("button").test(code(file)),
      `${file} must render its controls through Button`,
    ).toBe(false);
  });

  it.each(HAND_ROLLED_BUTTONS)("imports the catalog Button in %s", (file) => {
    expect(importsFromUi("Button").test(read(file)), `${file} renders a control`).toBe(true);
  });

  it.each(BUTTON_RECIPE_COPIES)(
    "leaves no copy of the button recipe in %s (%s)",
    (file, recipe) => {
      expect(code(file), `${file} must take this treatment from a Button variant`).not.toContain(
        recipe,
      );
    },
  );

  /*
   * The point of F3.T1's upgrade is the QUIET variants — a deny button and a
   * decline link are not primary actions, and before the upgrade the recipe had
   * no way to say so. An adoption that routed every control onto the default
   * `primary` would satisfy every assertion above and change nothing.
   */
  it("reaches for the variants the upgraded recipe added", () => {
    const source: string = SWEPT.map((file: string): string => code(file)).join("\n");
    const used: string[] = ["outline", "secondary", "ghost", "link"].filter((variant) =>
      source.includes(`variant="${variant}"`),
    );

    expect(used, "no quiet Button variant is used anywhere in the app").not.toEqual([]);
  });
});
