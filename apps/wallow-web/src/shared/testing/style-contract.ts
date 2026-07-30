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
