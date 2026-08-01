import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { SimpleSelect, type SimpleSelectOption } from "./simple-select";

/*
 * The boundary translations the stories cannot show, all assertable with the
 * popup CLOSED — the `browser` project loads no Tailwind, so a portalled
 * `role="option"` row measures 0x0 and a click on it hangs until Playwright's
 * actionability timeout. Opening the popup is the stories' job.
 *
 * Each assertion below is a bug fixed once already:
 *   - `testId` names the TRIGGER, the element an E2E suite clicks;
 *   - `""` on the caller's side is `null` in Base UI, so the placeholder shows
 *     rather than an empty selection;
 *   - `items` makes the trigger report the option's LABEL, not the wire value;
 *   - `label` is the trigger's accessible name, which it has none of otherwise.
 */

const STATUSES: readonly SimpleSelectOption[] = [
  { value: "open", label: "Open" },
  { value: "web-app", label: "Web Application" },
];

function renderSelect(value: string) {
  return render(
    <SimpleSelect
      testId="status-select"
      label="Status"
      value={value}
      options={STATUSES}
      onChange={() => {}}
      placeholder="Choose a status"
    />,
  );
}

function triggerOf(container: HTMLElement): HTMLElement {
  const trigger = container.querySelector('[data-testid="status-select"]');
  expect(trigger).not.toBeNull();
  return trigger as HTMLElement;
}

describe("SimpleSelect", () => {
  it("puts the caller's testId on the trigger", async () => {
    const { container } = await renderSelect("");

    expect(triggerOf(container).getAttribute("role")).toBe("combobox");
  });

  it("shows the placeholder when the caller's value is the empty string", async () => {
    const { container } = await renderSelect("");

    expect(triggerOf(container).textContent).toContain("Choose a status");
  });

  it("reports a chosen option by its label rather than its wire value", async () => {
    const { container } = await renderSelect("web-app");

    const text = triggerOf(container).textContent ?? "";
    expect(text).toContain("Web Application");
    expect(text).not.toContain("web-app");
  });

  it("names the trigger with the required label", async () => {
    const { container } = await renderSelect("");

    const labelledBy = triggerOf(container).getAttribute("aria-labelledby") ?? "";
    const names = labelledBy
      .split(" ")
      .filter((id: string) => id !== "")
      .map((id: string) => container.ownerDocument.getElementById(id)?.textContent ?? "");

    expect(names).toContain("Status");
  });
});
