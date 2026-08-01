/**
 * Driving the catalog `Select` from a spec.
 *
 * A `@bc-solutions-coder/ui` `Select` is not a native `<select>`: the trigger is
 * a `role="combobox"` button, and the options exist in the DOM — portalled onto
 * `<body>` — only while the popup is open. `userEvent.selectOptions` needs a real
 * `HTMLSelectElement` and cannot drive it, so picking a value is two clicks with
 * a settle in between. That sequence lives here so every spec spells it the same
 * way.
 *
 * Options are addressed by their ACCESSIBLE NAME rather than by a testid: the
 * name is what a user and a screen reader have to go on, and keying off it keeps
 * these specs from mandating testids on parts nothing else needs them on.
 */
import { page, userEvent } from "vitest/browser";
import { expect } from "vitest";

import { byTestId } from "./locators";

/**
 * Open the select identified by `triggerTestId` and choose the option with the
 * given accessible name.
 *
 * Both waits are load-bearing. `aria-expanded` flips a commit after the click
 * resolves, so querying the portalled option immediately finds nothing; and the
 * popup unmounts a frame after the choice, so a spec that reads the resulting
 * value too early races the close.
 */
export async function chooseOption(triggerTestId: string, optionName: string): Promise<void> {
  const trigger: HTMLElement = byTestId(triggerTestId);

  await userEvent.click(trigger);
  await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("true");

  await userEvent.click(page.getByRole("option", { name: optionName, exact: true }));
  await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("false");
}

/**
 * Assert that `testId` is the catalog `Select`'s trigger rather than a
 * hand-rolled native `<select>`.
 *
 * The three assertions are the listbox-combobox contract Base UI implements and
 * a native `<select>` does not expose to the a11y tree the same way: an
 * explicit `role="combobox"`, an `aria-haspopup="listbox"`, and a popup that is
 * genuinely absent (not merely hidden) until the trigger is activated.
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
