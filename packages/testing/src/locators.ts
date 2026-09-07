/**
 * Browser-only element lookup and DOM assertions for test IDs, descendants, and tags.
 */
import { page } from "vitest/browser";
import { expect } from "vitest";

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
 * has painted, so a spec gates on its page/state root once and then reads the
 * settled DOM.
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
 * An element's parent — several wrappers a spec needs to reach (the header row,
 * the list card surface) carry no testid, and inventing testids purely to reach
 * them would grow the contract the app ships.
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

/** `element` is rendered with the given tag (a restyle must not swap roles). */
export function expectTag(element: Element, tagName: string): void {
  expect(tagOf(element), `expected a <${tagName}>`).toBe(tagName.toLowerCase());
}
