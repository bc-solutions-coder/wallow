import { QueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "../form/app-form";
import { SubmitButton } from "../form/submit-button";
import { useAppForm } from "../form/use-app-form";

/*
 * `TextField` through the REAL pipeline, in the browser project (real headless
 * Chromium, a real `QueryClient`, the real `useAppForm` + `AppForm` +
 * `AppField` + ui `Field`/`Input` — nothing is mocked, per
 * .claude/rules/TESTING.md). A field mounted any other way would not prove the
 * thing that matters: that a form author writes `<f.TextField label="..." />`
 * and gets a labelled, testid-stamped, error-displaying control for free.
 *
 * This is the catalog's template spec — the other four fields mirror its cases:
 *
 *   1. The label is genuinely ASSOCIATED with the control (the ui `Field` row
 *      does it; nothing here keeps an htmlFor/id pair in sync by hand), and the
 *      control's testid is DERIVED from the form's prefix plus the field name.
 *   2. The passthrough props a migrated screen needs (type/placeholder/
 *      autoComplete), and the optional marker.
 *   3. A failed submit puts the schema's message under the field, at the
 *      derived `-error` id the E2E suites select.
 *   4. That message does not outlive the value that caused it.
 *   5. An explicit `testId` beats the derivation for BOTH ids — the migration
 *      compatibility valve (`forgot-password-email` over `demo-name`).
 *   6. The control is disabled while the submit is in flight, so a second edit
 *      cannot race the request.
 */

const schema = z.object({
  name: z.string().trim().min(1, "This field is required"),
});

type Values = z.output<typeof schema>;

interface HarnessProps {
  readonly type?: "email";
  readonly placeholder?: string;
  readonly autoComplete?: string;
  readonly inputMode?: "numeric";
  readonly optional?: boolean;
  readonly testId?: string;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

/** A form built the way a migrated screen builds one: hook, shell, one field. */
function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: { name: "" },
    onSubmit: props.onSubmit ?? ((): void => undefined),
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="name">
        {(field) => (
          <field.TextField
            label="Full name"
            type={props.type}
            placeholder={props.placeholder}
            autoComplete={props.autoComplete}
            inputMode={props.inputMode}
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

function input(container: HTMLElement, id = "demo-name"): HTMLInputElement {
  return byTestId(container, id) as HTMLInputElement;
}

function submitButton(container: HTMLElement): HTMLButtonElement {
  return byTestId(container, "demo-submit") as HTMLButtonElement;
}

/**
 * The text of whatever NAMES `control` — the elements its `aria-labelledby`
 * points at, or the `<label for>` pointing back at it. Asserting the name rather
 * than one specific attribute keeps the case about association (which is what a
 * screen reader and `getByLabelText` consume) instead of about which of the two
 * mechanisms Base UI happened to use for this control.
 */
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

/** A promise the spec settles by hand, so "in flight" is an observable state. */
function createDeferred(): { readonly promise: Promise<void>; readonly resolve: () => void } {
  let settle: () => void = () => undefined;
  const promise = new Promise<void>((resolve) => {
    settle = resolve;
  });

  return { promise, resolve: () => settle() };
}

describe("TextField", () => {
  it("associates the label with the input and derives the control testid", async () => {
    const { container } = await renderHarness();

    expect(input(container).tagName).toBe("INPUT");
    expect(input(container).type).toBe("text");
    expect(accessibleName(container, input(container))).toBe("Full name");
  });

  it("forwards type, placeholder and autoComplete to the input", async () => {
    const { container } = await renderHarness({
      type: "email",
      placeholder: "you@example.com",
      autoComplete: "email",
    });

    expect(input(container).type).toBe("email");
    expect(input(container).placeholder).toBe("you@example.com");
    expect(input(container).autocomplete).toBe("email");
  });

  it("asks for a numeric keypad without changing the input type", async () => {
    // The pair a zero-padded one-time code needs: `type="number"` would eat the
    // leading zero of "042317", so the digits-only hint has to travel separately.
    const { container } = await renderHarness({ inputMode: "numeric" });

    expect(input(container).type).toBe("text");
    expect(input(container).inputMode).toBe("numeric");
  });

  it("marks an optional field in its label", async () => {
    // Three of the forms being migrated mix required and optional fields in one
    // column; the marker is what tells them apart without a red asterisk legend.
    const { container } = await renderHarness({ optional: true });
    const label = container.querySelector("label");

    expect(label?.textContent).toContain("Full name");
    expect(label?.textContent?.toLowerCase()).toContain("optional");
  });

  it("shows the schema message at the derived error testid after a failed submit", async () => {
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("This field is required");
  });

  it("clears the message once the value is corrected and resubmitted", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("This field is required");

    await userEvent.fill(input(container), "Ada");
    await userEvent.click(submitButton(container));

    await expect.poll(() => queryTestId(container, "demo-name-error")).toBeNull();
    // The typed value reached form state, not just the DOM.
    await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({ name: "Ada" });
  });

  it("lets an explicit testId beat the derivation for the control and its message", async () => {
    // ForgotPasswordForm's `forgot-password-email` on a field named `name` under
    // a `demo` prefix: both halves are asserted because an implementation that
    // merely renamed the prefix would satisfy either one alone.
    const { container } = await renderHarness({ testId: "forgot-password-email" });

    expect(input(container, "forgot-password-email").tagName).toBe("INPUT");
    expect(queryTestId(container, "demo-name")).toBeNull();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "forgot-password-email-error")?.textContent)
      .toBe("This field is required");
    expect(queryTestId(container, "demo-name-error")).toBeNull();
  });

  it("disables the input while the submit is in flight", async () => {
    const deferred = createDeferred();
    const { container } = await renderHarness({ onSubmit: () => deferred.promise });

    await userEvent.fill(input(container), "Ada");
    await userEvent.click(submitButton(container));

    await expect.poll(() => input(container).disabled).toBe(true);

    deferred.resolve();

    await expect.poll(() => input(container).disabled).toBe(false);
  });
});
