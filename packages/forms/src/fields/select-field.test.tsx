import { render } from "@bc-solutions-coder/testing/render";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "../form/app-form";
import { SubmitButton } from "../form/submit-button";
import { useAppForm } from "../form/use-app-form";
import type { SelectFieldOption } from "./select-field";

/*
 * `SelectField` through the REAL pipeline, in the browser project (nothing
 * mocked — see `text-field.test.tsx`, the catalog's template spec, for the full
 * rationale and the shape of these cases).
 *
 * What this field adds over the template:
 *
 *   - The control is the catalog `Select` — a `role="combobox"` trigger over a
 *     portalled listbox — NOT a native `<select>`. That is asserted explicitly
 *     because the four call sites being migrated were hand-rolled `<select>`s
 *     until recently, and a regression back to one would still pass every other
 *     case here.
 *   - The field name is `projectType`, so the derived id also pins the
 *     camelCase -> kebab-case fold (`demo-project-type`) that
 *     CreateInquiryForm's existing testids depend on.
 *   - "Nothing chosen" is `""` in form state and `null` in Base UI; choosing an
 *     option has to land the WIRE VALUE (`web-app`) in form state while the
 *     trigger shows the LABEL ("Web application").
 */

const OPTIONS: readonly SelectFieldOption[] = [
  { value: "web-app", label: "Web application" },
  { value: "mobile-app", label: "Mobile application" },
];

const schema = z.object({
  projectType: z.string().min(1, "Choose a project type"),
});

type Values = z.output<typeof schema>;

interface HarnessProps {
  readonly optional?: boolean;
  readonly testId?: string;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: { projectType: "" },
    onSubmit: props.onSubmit ?? ((): void => undefined),
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="projectType">
        {(field) => (
          <field.SelectField
            label="Project type"
            options={OPTIONS}
            placeholder="Select..."
            optional={props.optional}
            testId={props.testId}
          />
        )}
      </form.AppField>
      <SubmitButton>Save</SubmitButton>
    </AppForm>
  );
}

/** Each case gets its own client so no mutation state leaks between them. */
function renderHarness(props: HarnessProps = {}) {
  const client = new QueryClient({
    defaultOptions: { mutations: { retry: false }, queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <Harness {...props} />
    </QueryClientProvider>,
  );
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();
  return element as HTMLElement;
}

function queryTestId(container: HTMLElement, id: string): HTMLElement | null {
  return container.querySelector(`[data-testid="${id}"]`);
}

function trigger(container: HTMLElement, id = "demo-project-type"): HTMLElement {
  return byTestId(container, id);
}

function submitButton(container: HTMLElement): HTMLButtonElement {
  return byTestId(container, "demo-submit") as HTMLButtonElement;
}

/** See `text-field.test.tsx` — the text of whatever names `control`. */
function accessibleName(container: HTMLElement, control: HTMLElement): string {
  const labelledBy = control.getAttribute("aria-labelledby");
  const document = container.ownerDocument;

  if (labelledBy !== null && labelledBy !== "") {
    return labelledBy
      .split(" ")
      .map((id: string) => document.getElementById(id)?.textContent?.trim() ?? "")
      .filter((text: string) => text !== "")
      .join(" ");
  }

  const label =
    control.id === "" ? null : document.querySelector(`label[for="${CSS.escape(control.id)}"]`);

  return label?.textContent?.trim() ?? "";
}

/**
 * Open the select and choose an option by its accessible name.
 *
 * Both waits are load-bearing: `aria-expanded` flips a commit after the click
 * resolves, so querying the portalled option immediately finds nothing, and the
 * popup unmounts a frame after the choice, so reading the resulting value too
 * early races the close. (Same sequence as wallow-web's `chooseOption` helper.)
 */
async function chooseOption(control: HTMLElement, optionName: string): Promise<void> {
  await userEvent.click(control);
  await expect.poll(() => control.getAttribute("aria-expanded")).toBe("true");

  await userEvent.click(page.getByRole("option", { name: optionName, exact: true }));
  await expect.poll(() => control.getAttribute("aria-expanded")).toBe("false");
}

describe("SelectField", () => {
  it("renders the catalog Select trigger, labelled, at the kebab-cased derived testid", async () => {
    const { container } = await renderHarness();

    // The listbox-combobox contract Base UI implements and a native <select>
    // does not: an explicit role, a listbox popup, and a popup that is genuinely
    // absent rather than merely hidden until the trigger is activated.
    expect(trigger(container).tagName).not.toBe("SELECT");
    expect(trigger(container).getAttribute("role")).toBe("combobox");
    expect(trigger(container).getAttribute("aria-haspopup")).toBe("listbox");
    expect(trigger(container).getAttribute("aria-expanded")).toBe("false");
    expect(accessibleName(container, trigger(container))).toBe("Project type");
    // Nothing chosen yet, so the trigger reads as the placeholder.
    expect(trigger(container).textContent).toContain("Select...");
  });

  it("marks an optional field in its label", async () => {
    const { container } = await renderHarness({ optional: true });
    const label = container.querySelector("label");

    expect(label?.textContent).toContain("Project type");
    expect(label?.textContent?.toLowerCase()).toContain("optional");
  });

  it("shows the schema message at the derived error testid after a failed submit", async () => {
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "demo-project-type-error")?.textContent)
      .toBe("Choose a project type");
  });

  it("puts the chosen option's value in form state and clears the message", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-project-type-error")?.textContent)
      .toBe("Choose a project type");

    await chooseOption(trigger(container), "Web application");

    // The trigger reads as the LABEL; the WIRE VALUE is what submits.
    expect(trigger(container).textContent).toContain("Web application");

    await userEvent.click(submitButton(container));

    await expect.poll(() => queryTestId(container, "demo-project-type-error")).toBeNull();
    await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({ projectType: "web-app" });
  });

  it("lets an explicit testId beat the derivation for the trigger and its message", async () => {
    // CreateInquiryForm's own id, on a field named `projectType` under a `demo` prefix.
    const { container } = await renderHarness({ testId: "inquiry-project-type" });

    expect(trigger(container, "inquiry-project-type").getAttribute("role")).toBe("combobox");
    expect(queryTestId(container, "demo-project-type")).toBeNull();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "inquiry-project-type-error")?.textContent)
      .toBe("Choose a project type");
    expect(queryTestId(container, "demo-project-type-error")).toBeNull();
  });
});
