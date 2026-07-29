import { render } from "@bc-solutions-coder/testing/render";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "../form/app-form";
import { SubmitButton } from "../form/submit-button";
import { useAppForm } from "../form/use-app-form";

/*
 * `TextareaField` through the REAL pipeline, in the browser project (nothing
 * mocked — see `text-field.test.tsx`, the catalog's template spec, for the full
 * rationale and the shape of these cases).
 *
 * What this field adds over the template: the control is a real `<textarea>`
 * (the ui `Textarea`, not an `Input`), because Base UI ships no textarea part and
 * a field that quietly rendered a single-line box would still pass every other
 * assertion here. CreateInquiryForm's message box is the migration target.
 */

const schema = z.object({
  message: z.string().trim().min(1, "Tell us about your project"),
});

type Values = z.output<typeof schema>;

interface HarnessProps {
  readonly placeholder?: string;
  readonly rows?: number;
  readonly optional?: boolean;
  readonly testId?: string;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: { message: "" },
    onSubmit: props.onSubmit ?? ((): void => undefined),
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="message">
        {(field) => (
          <field.TextareaField
            label="Message"
            placeholder={props.placeholder}
            rows={props.rows}
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

function textarea(container: HTMLElement, id = "demo-message"): HTMLTextAreaElement {
  return byTestId(container, id) as HTMLTextAreaElement;
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

describe("TextareaField", () => {
  it("associates the label with a real textarea and derives the control testid", async () => {
    const { container } = await renderHarness({ rows: 5, placeholder: "A few sentences" });

    expect(textarea(container).tagName).toBe("TEXTAREA");
    expect(textarea(container).rows).toBe(5);
    expect(textarea(container).placeholder).toBe("A few sentences");
    expect(accessibleName(container, textarea(container))).toBe("Message");
  });

  it("marks an optional field in its label", async () => {
    const { container } = await renderHarness({ optional: true });
    const label = container.querySelector("label");

    expect(label?.textContent).toContain("Message");
    expect(label?.textContent?.toLowerCase()).toContain("optional");
  });

  it("shows the schema message at the derived error testid after a failed submit", async () => {
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "demo-message-error")?.textContent)
      .toBe("Tell us about your project");
  });

  it("clears the message once the value is corrected and resubmitted", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-message-error")?.textContent)
      .toBe("Tell us about your project");

    await userEvent.fill(textarea(container), "We need a portal.");
    await userEvent.click(submitButton(container));

    await expect.poll(() => queryTestId(container, "demo-message-error")).toBeNull();
    await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({ message: "We need a portal." });
  });

  it("lets an explicit testId beat the derivation for the control and its message", async () => {
    // CreateInquiryForm's own id, on a field named `message` under a `demo` prefix.
    const { container } = await renderHarness({ testId: "inquiry-message" });

    expect(textarea(container, "inquiry-message").tagName).toBe("TEXTAREA");
    expect(queryTestId(container, "demo-message")).toBeNull();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "inquiry-message-error")?.textContent)
      .toBe("Tell us about your project");
    expect(queryTestId(container, "demo-message-error")).toBeNull();
  });
});
