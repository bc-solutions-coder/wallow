/**
 * Shared assertions for the dashboard restyle (Wallow-urec.4) — written for
 * `.4.1` (apps) as the worked example the inquiries/organizations/settings
 * restyles reuse against their own pages.
 *
 * The restyle is styling-only, so a `*.restyle.test.tsx` spec asserts exactly
 * two things: the recipe's token classes landed on the recipe's elements, and no
 * literal color leaked in while porting the old Blazor markup (which hard-coded
 * `#d4a017` / `#3d2b1f` / `#e8d5b7` everywhere). Behaviour stays pinned by each
 * component's pre-existing spec, which a restyle must leave untouched.
 */
import { page } from "vitest/browser";
import { expect } from "vitest";

import { PAGE_CONTAINER } from "../lib/page-container";

/** Tailwind's default palette hues — none of which are Wallow design tokens. */
const PALETTE_HUES =
  "slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose";

/** An arbitrary-value color, e.g. `bg-[#d4a017]` or `text-[#3d2b1f]/60`. */
const ARBITRARY_COLOR = /\[#[\da-f]{3,8}\]/iu;

/** A raw palette utility, e.g. `bg-white`, `border-amber-300`, `text-gray-500/70`. */
const PALETTE_COLOR = new RegExp(
  String.raw`^[a-z-]+-(?:white|black|(?:${PALETTE_HUES})-\d{2,3})(?:/\d{1,3})?$`,
  "u",
);

function tagOf(element: Element): string {
  return element.tagName.toLowerCase();
}

function describeElement(element: Element): string {
  return `<${tagOf(element)} class="${element.getAttribute("class") ?? ""}">`;
}

/** The single element carrying `testId`; fails loudly when absent or duplicated. */
export function byTestId(testId: string): HTMLElement {
  const elements = page.getByTestId(testId).elements();
  expect(elements, `expected exactly one [data-testid="${testId}"]`).toHaveLength(1);
  return elements[0] as HTMLElement;
}

/**
 * Wait for `testId` to commit, then return it. `render()` returns before React
 * has painted, so a restyle spec gates on its page/state root once and then
 * reads classes off the settled DOM synchronously.
 */
export async function waitForTestId(testId: string): Promise<HTMLElement> {
  await expect.element(page.getByTestId(testId)).toBeInTheDocument();
  return byTestId(testId);
}

/** Every element carrying `testId`, in document order (list rows). */
export function allByTestId(testId: string): HTMLElement[] {
  return page.getByTestId(testId).elements() as HTMLElement[];
}

/**
 * An element's parent — the recipe styles several wrappers (the header row, the
 * list card surface) that carry no testid of their own, and inventing testids
 * purely to assert styling would grow the contract the restyle promised not to
 * touch.
 */
export function parentOf(element: Element): HTMLElement {
  const parent = element.parentElement;
  expect(parent, `${describeElement(element)} has no parent element`).not.toBeNull();
  return parent as HTMLElement;
}

/** The first descendant matching `selector`, scoped to `root`. */
export function within(root: Element, selector: string): HTMLElement {
  const found = root.querySelector(selector);
  expect(found, `${describeElement(root)} has no descendant matching "${selector}"`).not.toBeNull();
  return found as HTMLElement;
}

/** Every class in the space-separated `recipe` is present on `element`. */
export function expectClasses(element: Element, recipe: string): void {
  const missing = recipe
    .split(/\s+/u)
    .filter((cls) => cls !== "" && !element.classList.contains(cls));
  expect(missing, `${describeElement(element)} is missing recipe classes`).toEqual([]);
}

/** Any Tailwind max-width utility, e.g. `max-w-5xl`, `max-w-2xl`, `max-w-[42rem]`. */
const MAX_WIDTH_UTILITY = /^max-w-/u;

/**
 * `root` takes its content width from the shared `PAGE_CONTAINER` rule and from
 * nothing else (Wallow-lrlm.5.1).
 *
 * Two halves, and the second is the one that matters: asserting the shared
 * classes are PRESENT would still pass on a page that also carried a width of
 * its own, which is exactly the "each page picks its own" state F5.T1 removes.
 * So any `max-w-*` utility outside the shared rule is an offender.
 */
export function expectPageContainer(root: Element): void {
  expectClasses(root, PAGE_CONTAINER);

  const shared = new Set(PAGE_CONTAINER.split(/\s+/u).filter((cls) => cls !== ""));
  const strays = [...root.classList].filter(
    (cls) => MAX_WIDTH_UTILITY.test(cls) && !shared.has(cls),
  );
  expect(strays, `${describeElement(root)} must take its width from PAGE_CONTAINER alone`).toEqual(
    [],
  );
}

/*
 * The catalog recipes wallow-web's lists, empty states and status chips migrate
 * onto (Wallow-lrlm.5.2). Each constant is the recipe's own class string, copied
 * from `packages/ui/src/components/*\/*.styles.ts` — a spec asserts against the
 * recipe rather than against a string this app maintains, so a catalog restyle
 * shows up here as a failing spec instead of as silent drift.
 */

/** `listCardRecipe()` — the card surface `ListCard` renders around its `<ul>`. */
export const LIST_CARD_SURFACE =
  "bg-card rounded-lg shadow-sm border border-border overflow-hidden";

/** `listCardListRecipe()` — the divided `<ul>` the surface clips. */
export const LIST_CARD_LIST = "divide-y divide-border";

/**
 * `listRowRecipe()` — the row cell. Two departures from the string the apps used
 * to hand-roll, both decided in Wallow-lrlm.3.5: `hover:bg-background/50` became
 * `hover:bg-muted`, and the row gained the catalog focus indicator because a
 * row composed with a `Link` is a tab stop.
 */
export const LIST_ROW =
  "flex items-center justify-between px-6 py-4 outline-none motion-safe:transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring";

/** `badgeRecipe()`'s base — the pill SHAPE, which no variant changes. */
export const BADGE_SHAPE = "inline-block text-xs font-medium px-2.5 py-0.5 rounded-full";

/**
 * `badgeRecipe()`'s variants. Each paints BOTH halves of a token pair, so a spec
 * can assert one variant landed AND that no sibling variant's surface did.
 */
export const BADGE_VARIANTS = {
  neutral: "bg-accent text-accent-foreground",
  success: "bg-success text-success-foreground",
  warning: "bg-primary text-primary-foreground",
  destructive: "bg-destructive text-destructive-foreground",
} as const;

/** A `Badge` variant name. */
export type BadgeVariant = keyof typeof BADGE_VARIANTS;

/** `emptyStateRecipe()` — the spacing block `EmptyState` hands `Card`. */
export const EMPTY_STATE_SPACING = "p-12 flex flex-col items-center gap-2 text-center";

/** `cardRecipe()` — the surface `EmptyState` composes underneath that block. */
export const CARD_SURFACE = "rounded-lg border border-border bg-card";

/** `emptyStateIconRecipe()` — the decorative glyph slot above the message. */
export const EMPTY_STATE_ICON = "text-7xl leading-none mb-2";

/**
 * `list` is the `<ul>` a catalog `ListCard` renders, clipped by the card surface
 * `ListCard` puts around it. Both halves matter: the surface is what the app
 * used to hand-roll, and the `<ul>` is what carries the shipped `X-table` testid.
 */
export function expectListCard(list: Element): void {
  expectTag(list, "ul");
  expectClasses(list, LIST_CARD_LIST);

  const surface = parentOf(list);
  expectTag(surface, "div");
  expectClasses(surface, LIST_CARD_SURFACE);
}

/**
 * `row` is a catalog `ListRow`. `tagName` says which element it resolved to —
 * `"li"` for a plain row, `"a"` when the caller composed a router `Link` through
 * `render`, which SUBSTITUTES the element rather than wrapping it.
 */
export function expectListRow(row: Element, tagName: string): void {
  expectTag(row, tagName);
  expectClasses(row, LIST_ROW);
}

/**
 * `element` is a catalog `Badge` in `variant`. The negative half is the
 * load-bearing one: the shape alone is variant-independent, so asserting only
 * the presence of `variant`'s classes would pass an element that also carried a
 * second variant's surface.
 */
export function expectBadge(element: Element, variant: BadgeVariant): void {
  expectTag(element, "span");
  expectClasses(element, BADGE_SHAPE);
  expectClasses(element, BADGE_VARIANTS[variant]);

  const foreign = Object.entries(BADGE_VARIANTS)
    .filter(([name]) => name !== variant)
    .flatMap(([, classes]) => classes.split(/\s+/u))
    .filter((cls) => element.classList.contains(cls));
  expect(foreign, `${describeElement(element)} must carry only the ${variant} variant`).toEqual([]);
}

/** The slots an `EmptyState` renders, each keyed by its derived test id suffix. */
export interface EmptyStateParts {
  /** The decorative glyph, or `null` when the card renders none. */
  readonly icon: string | null;
  /** The `<h2>` sentence. */
  readonly message: string;
  /** The supporting copy, or `null` when the card renders none. */
  readonly description: string | null;
}

/**
 * `root` is a catalog `EmptyState` carrying `testId`, with the given slots. The
 * slots are addressed by the test ids `EmptyState` DERIVES from the root's, so
 * this also pins that an app names the block once.
 *
 * A slot given as `null` must be ABSENT, not empty: `EmptyState` omits an unused
 * slot entirely so it leaves no element behind to collect the column gap.
 */
export function expectEmptyState(root: Element, testId: string, parts: EmptyStateParts): void {
  expectTag(root, "div");
  expectClasses(root, CARD_SURFACE);
  expectClasses(root, EMPTY_STATE_SPACING);

  const message = within(root, `[data-testid="${testId}-message"]`);
  expectTag(message, "h2");
  expect(message.textContent).toBe(parts.message);
  expectClasses(message, "text-xl font-semibold text-foreground");

  expectOptionalSlot(root, `${testId}-icon`, parts.icon, EMPTY_STATE_ICON);
  expectOptionalSlot(root, `${testId}-description`, parts.description, "text-muted-foreground");
}

/** One optional `EmptyState` slot: present with `text` and `recipe`, or absent. */
function expectOptionalSlot(
  root: Element,
  testId: string,
  text: string | null,
  recipe: string,
): void {
  const found = root.querySelector(`[data-testid="${testId}"]`);
  if (text === null) {
    expect(found, `[data-testid="${testId}"] must be omitted, not rendered empty`).toBeNull();
    return;
  }
  expect(found, `${describeElement(root)} has no [data-testid="${testId}"]`).not.toBeNull();
  expect((found as HTMLElement).textContent).toBe(text);
  expectClasses(found as HTMLElement, recipe);
}

/** `element` is rendered with the given tag (the restyle must not swap roles). */
export function expectTag(element: Element, tagName: string): void {
  expect(tagOf(element), `expected a <${tagName}>`).toBe(tagName.toLowerCase());
}

/** `first` appears before `second` in document order. */
export function expectPrecedes(first: Element, second: Element): void {
  const following = Boolean(
    first.compareDocumentPosition(second) & Node.DOCUMENT_POSITION_FOLLOWING,
  );
  expect(following, `${describeElement(first)} should precede ${describeElement(second)}`).toBe(
    true,
  );
}

/**
 * No literal color anywhere in `root`'s subtree — the restyle must express the
 * cream/brown/gold palette through the theme tokens (`bg-card`, `text-foreground`,
 * `bg-primary`, `border-border`, `bg-accent`, …) so a fork's `branding.json`
 * still drives the page.
 */
export function expectTokenColorsOnly(root: Element): void {
  const offenders = new Set<string>();
  for (const element of [root, ...root.querySelectorAll("*")]) {
    for (const cls of element.classList) {
      // Strip any variant prefixes (`hover:`, `md:`) before matching the utility.
      const utility = cls.slice(cls.lastIndexOf(":") + 1);
      if (ARBITRARY_COLOR.test(utility) || PALETTE_COLOR.test(utility)) {
        offenders.add(cls);
      }
    }
  }
  expect(
    [...offenders],
    "restyle must use theme tokens, not hard-coded colors from the old Blazor markup",
  ).toEqual([]);
}
