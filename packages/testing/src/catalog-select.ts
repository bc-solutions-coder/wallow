/**
 * Browser-only helpers for driving the catalog Select through its portalled options.
 */
import { page, userEvent } from "vitest/browser";
import { expect } from "vitest";

import { byTestId } from "./locators";

/**
 * Open a catalog Select and choose the option with an exact accessible name.
 *
 * Waits for the trigger's aria-expanded state before selecting and after closing.
 * The trigger must already be mounted and carry triggerTestId.
 */
export async function chooseOption(triggerTestId: string, optionName: string): Promise<void> {
  const trigger: HTMLElement = byTestId(triggerTestId);

  await userEvent.click(trigger);
  await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("true");

  await userEvent.click(page.getByRole("option", { name: optionName, exact: true }));
  await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("false");
}

/**
 * Assert that a catalog Select trigger has its closed combobox attributes.
 *
 * Checks the element type, role, listbox popup type, and aria-expanded=false.
 * It does not inspect whether popup elements are mounted.
 */
export function expectCatalogSelect(testId: string): void {
  const trigger: HTMLElement = byTestId(testId);

  expect(trigger.tagName, `[data-testid="${testId}"] should not be a native <select>`).not.toBe(
    "SELECT",
  );
  expect(trigger.getAttribute("role")).toBe("combobox");
  expect(trigger.getAttribute("aria-haspopup")).toBe("listbox");
  expect(trigger.getAttribute("aria-expanded")).toBe("false");
}
