import { QueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import { render } from "@bc-solutions-coder/testing/render";
import type { ReactNode } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "../form/app-form";
import { SubmitButton } from "../form/submit-button";
import { useAppForm } from "../form/use-app-form";

/*
 * `PasswordField` through the REAL pipeline, in the browser project (nothing
 * mocked — see `text-field.test.tsx`, the catalog's template spec, for the full
 * rationale and the shape of these cases).
 *
 * The one case this field adds over the template: the control is MASKED, and it
 * is masked because the component says so rather than because a caller passed
 * the right `type`. ResetPasswordForm's two boxes are the migration target.
 */

const schema = z.object({
  password: z.string().min(8, "Password must be at least 8 characters"),
});

type Values = z.output<typeof schema>;

interface HarnessProps {
  readonly placeholder?: string;
  readonly autoComplete?: string;
  readonly labelAction?: ReactNode;
  readonly testId?: string;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: { password: "" },
    onSubmit: props.onSubmit ?? ((): void => undefined),
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="password">
        {(field) => (
          <field.PasswordField
            label="New password"
            placeholder={props.placeholder}
            autoComplete={props.autoComplete}
            labelAction={props.labelAction}
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

function input(container: HTMLElement, id = "demo-password"): HTMLInputElement {
  return byTestId(container, id) as HTMLInputElement;
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

describe("PasswordField", () => {
  it("renders a masked input with its label associated and the derived testid", async () => {
    const { container } = await renderHarness({ autoComplete: "new-password" });

    expect(input(container).tagName).toBe("INPUT");
    // The whole reason this field exists rather than `<TextField type="password">`.
    expect(input(container).type).toBe("password");
    expect(input(container).autocomplete).toBe("new-password");
    expect(accessibleName(container, input(container))).toBe("New password");
  });

  it("puts a labelAction beside the label without folding it into the accessible name", async () => {
    // The sign-in screen's "Forgot password?" link. An anchor inside the label
    // would name the field after it AND sit inside the box's own click area.
    const { container } = await renderHarness({
      labelAction: <a href="/forgot-password">Forgot password?</a>,
    });

    expect(container.querySelector("a")?.textContent).toBe("Forgot password?");
    expect(accessibleName(container, input(container))).toBe("New password");
  });

  it("shows the schema message at the derived error testid after a failed submit", async () => {
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "demo-password-error")?.textContent)
      .toBe("Password must be at least 8 characters");
  });

  it("clears the message once the value is corrected and resubmitted", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-password-error")?.textContent)
      .toBe("Password must be at least 8 characters");

    await userEvent.fill(input(container), "correct-horse");
    await userEvent.click(submitButton(container));

    await expect.poll(() => queryTestId(container, "demo-password-error")).toBeNull();
    await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
    expect(onSubmit.mock.calls[0]?.[0]).toEqual({ password: "correct-horse" });
  });

  it("lets an explicit testId beat the derivation for the control and its message", async () => {
    // ResetPasswordForm's own id, on a field named `password` under a `demo` prefix.
    const { container } = await renderHarness({ testId: "reset-password-new-password" });

    expect(input(container, "reset-password-new-password").type).toBe("password");
    expect(queryTestId(container, "demo-password")).toBeNull();

    await userEvent.click(submitButton(container));

    await expect
      .poll(() => queryTestId(container, "reset-password-new-password-error")?.textContent)
      .toBe("Password must be at least 8 characters");
    expect(queryTestId(container, "demo-password-error")).toBeNull();
  });
});
