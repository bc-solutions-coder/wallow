import { QueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "../form/app-form";
import { SubmitButton } from "../form/submit-button";
import { useAppForm } from "../form/use-app-form";

/*
 * `CheckboxField` through the REAL pipeline, in the browser project (nothing
 * mocked — see `text-field.test.tsx`, the catalog's template spec, for the full
 * rationale and the shape of these cases).
 *
 * What this field adds over the template: the value is a BOOLEAN and the control
 * is Base UI's checkbox, which reports through `onCheckedChange` rather than an
 * `onChange` event — so "ticking the box lands `true` in form state" is its own
 * case rather than something the text fields' `fill` already covered. The label
 * sits to the RIGHT of the box, and naming still has to work from there.
 */

const schema = z.object({
  acceptedTerms: z.boolean().refine((value: boolean) => value, "You must accept the terms"),
});

type Values = z.output<typeof schema>;

interface HarnessProps {
  readonly description?: string;
  readonly testId?: string;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: { acceptedTerms: false },
    onSubmit: props.onSubmit ?? ((): void => undefined),
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="acceptedTerms">
        {(field) => (
          <field.CheckboxField
            label="Accept the terms"
            description={props.description}
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

function box(container: HTMLElement, id = "demo-accepted-terms"): HTMLElement {
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

describe("CheckboxField", () => {
  it("names the box from its label and derives the control testid", async () => {
    const { container } = await renderHarness({ description: "You can withdraw consent later." });

    expect(box(container).getAttribute("role")).toBe("checkbox");
    expect(box(container).getAttribute("aria-checked")).toBe("false");
    expect(accessibleName(container, box(container))).toBe("Accept the terms");
    expect(container.textContent).toContain("You can withdraw consent later.");
  });

  it("shows the schema message at the derived error testid after a failed submit", async () => {
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "demo-accepted-terms-error")?.textContent)
      .toBe("You must accept the terms");
  });

  it("puts true in form state when ticked and clears the message", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-accepted-terms-error")?.textContent)
      .toBe("You must accept the terms");

    await userEvent.click(box(container));
    await expect.poll(() => box(container).getAttribute("aria-checked")).toBe("true");

    await userEvent.click(submitButton(container));

    await expect.poll(() => queryTestId(container, "demo-accepted-terms-error")).toBeNull();
    await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({ acceptedTerms: true });
  });

  it("lets an explicit testId beat the derivation for the box and its message", async () => {
    const { container } = await renderHarness({ testId: "app-accept-terms" });

    expect(box(container, "app-accept-terms").getAttribute("role")).toBe("checkbox");
    expect(queryTestId(container, "demo-accepted-terms")).toBeNull();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "app-accept-terms-error")?.textContent)
      .toBe("You must accept the terms");
    expect(queryTestId(container, "demo-accepted-terms-error")).toBeNull();
  });
});
