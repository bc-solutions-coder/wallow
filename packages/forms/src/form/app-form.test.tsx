import { consumeConsoleNoise } from "@bc-solutions-coder/testing/console-guard";
import { render } from "@bc-solutions-coder/testing/render";
import { useForm } from "@tanstack/react-form";
import { Component, type ReactNode } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { AppForm } from "./app-form";
import { useAppFormContext } from "./app-form-context";
import { FormError } from "./form-error";
import { SubmitButton } from "./submit-button";

/*
 * The AppForm shell + the two components that read its context, in the browser
 * project (real headless Chromium, nothing mocked — the ui Button and
 * ErrorBanner render for real, per .claude/rules/TESTING.md).
 *
 * What is pinned here, and why each case exists:
 *
 *   1. The `<form>` element itself: its derived `{prefix}-form` testid, the
 *      EXPLICIT testId override that beats the derivation, `noValidate`, and the
 *      vertical rhythm. The override is not a nicety: three of the five forms
 *      this package is replacing stamp the element with a prefix their fields do
 *      not share — `inquiry-create-form` alongside `inquiry-name`,
 *      `organization-create-form` alongside `organization-name` — and those ids
 *      are pinned by existing component specs and Playwright suites that must
 *      keep passing unchanged.
 *   2. The submit boilerplate the shell absorbs: `preventDefault` (a form that
 *      navigates loses the SPA) and `stopPropagation` (a form nested in another
 *      handler must not double-submit). Both are asserted through real events
 *      rather than by reading the source.
 *   3. `SubmitButton` and `FormError` reading `pending`/`serverError` off the
 *      context instead of props — that is the whole reason the shell publishes a
 *      context — plus their own testId overrides, which the same three forms
 *      need (`organization-create-submit`, `app-register-error`).
 *
 * The shell is driven by a REAL TanStack form built in `Harness` below: the
 * catalog fields do not exist yet (Wallow-ov6w.2.4), so submission is driven
 * through the shell's own submit button, which is exactly how a migrated form
 * will drive it.
 */

interface HarnessValues {
  email: string;
}

interface HarnessProps {
  readonly onSubmit?: (values: HarnessValues) => void;
  readonly testIdPrefix?: string;
  readonly testId?: string;
  readonly pending?: boolean;
  readonly pendingLabel?: string;
  readonly serverError?: string | null;
  readonly className?: string;
  readonly submitTestId?: string;
  readonly errorTestId?: string;
}

/**
 * A minimal real form rendered through the shell. Everything a caller can vary
 * is a prop so each case reads as one `render(<Harness ... />)` line.
 */
function Harness(props: HarnessProps) {
  const form = useForm({
    defaultValues: { email: "ada@example.com" },
    onSubmit: ({ value }: { value: HarnessValues }) => {
      props.onSubmit?.(value);
    },
  });

  return (
    <AppForm
      form={form}
      testIdPrefix={props.testIdPrefix ?? "demo"}
      testId={props.testId}
      pending={props.pending}
      serverError={props.serverError}
      className={props.className}
    >
      <FormError testId={props.errorTestId} />
      <SubmitButton pendingLabel={props.pendingLabel} testId={props.submitTestId}>
        Send
      </SubmitButton>
    </AppForm>
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

describe("AppForm", () => {
  describe("the form element", () => {
    it("renders a native form carrying the derived {prefix}-form testid", async () => {
      const { container } = await render(<Harness testIdPrefix="demo" />);

      expect(byTestId(container, "demo-form").tagName).toBe("FORM");
    });

    it("lets an explicit testId beat the derived id while children keep the prefix", async () => {
      // CreateInquiryForm's shape: the element is `inquiry-create-form` but its
      // fields, submit and error all derive from the bare `inquiry` prefix. Both
      // halves are asserted together because an implementation that simply
      // renamed the prefix would satisfy either one alone.
      const { container } = await render(
        <Harness testIdPrefix="inquiry" testId="inquiry-create-form" />,
      );

      expect(byTestId(container, "inquiry-create-form").tagName).toBe("FORM");
      expect(queryTestId(container, "inquiry-form")).toBeNull();
      expect(byTestId(container, "inquiry-submit").tagName).toBe("BUTTON");
    });

    it("turns off the browser's own validation so the schema owns the messages", async () => {
      // The fields validate through zod (Wallow-ov6w.2.3). Left on, a
      // `type="email"` control would double-validate and show a native bubble
      // that no spec can read.
      const { container } = await render(<Harness />);

      expect((byTestId(container, "demo-form") as HTMLFormElement).noValidate).toBe(true);
    });

    it("stacks its children with a default vertical rhythm", async () => {
      const { container } = await render(<Harness />);

      expect(byTestId(container, "demo-form").classList.contains("space-y-5")).toBe(true);
    });

    it("lets a caller className replace the default rhythm", async () => {
      // The migrated forms are not uniform — `space-y-4`, `space-y-5` and
      // `space-y-6` all appear today — so the default has to be replaceable
      // rather than merely appended to.
      const { container } = await render(<Harness className="space-y-6" />);

      const form = byTestId(container, "demo-form");
      expect(form.classList.contains("space-y-6")).toBe(true);
      expect(form.classList.contains("space-y-5")).toBe(false);
    });

    it("renders its children inside the form element", async () => {
      const { container } = await render(<Harness serverError="Could not submit." />);

      const form = byTestId(container, "demo-form");
      expect(form.querySelector('[data-testid="demo-submit"]')).not.toBeNull();
      expect(form.querySelector('[data-testid="demo-error"]')).not.toBeNull();
    });
  });

  describe("submission", () => {
    it("runs the form's submit handler and prevents the native navigation", async () => {
      // A submit that is not prevented navigates the browser-mode iframe, which
      // is why `defaultPrevented` is read off the real event rather than trusted
      // from the source. The listener is attached to the form element itself, so
      // it fires in the target phase BEFORE React's root handler — the flag is
      // therefore read after the dispatch has completed, not inside the listener.
      const onSubmit = vi.fn<(values: HarnessValues) => void>();
      const { container } = await render(<Harness onSubmit={onSubmit} />);

      const seen: { event: Event | null } = { event: null };
      byTestId(container, "demo-form").addEventListener("submit", (event: Event) => {
        seen.event = event;
      });

      await userEvent.click(byTestId(container, "demo-submit"));

      await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
      expect(onSubmit.mock.calls[0]?.[0]).toEqual({ email: "ada@example.com" });
      expect(seen.event).not.toBeNull();
      expect(seen.event?.defaultPrevented).toBe(true);
    });

    it("stops the submit from reaching an outer handler", async () => {
      // The `stopPropagation` half of the boilerplate: a form rendered inside
      // another submit handler must not fire it too. Asserted after the inner
      // submit has demonstrably completed, so the outer spy has had its chance —
      // React dispatches synthetic bubbling synchronously.
      const onSubmit = vi.fn<(values: HarnessValues) => void>();
      const onOuterSubmit = vi.fn();
      const { container } = await render(
        <div onSubmit={onOuterSubmit}>
          <Harness onSubmit={onSubmit} />
        </div>,
      );

      await userEvent.click(byTestId(container, "demo-submit"));

      await expect.poll(() => onSubmit.mock.calls.length).toBe(1);
      expect(onOuterSubmit).not.toHaveBeenCalled();
    });
  });

  describe("SubmitButton", () => {
    it("renders an enabled submit button with the derived {prefix}-submit testid", async () => {
      const { container } = await render(<Harness />);

      const button = byTestId(container, "demo-submit") as HTMLButtonElement;
      expect(button.tagName).toBe("BUTTON");
      expect(button.type).toBe("submit");
      expect(button.disabled).toBe(false);
      expect(button.textContent).toBe("Send");
    });

    it("disables and swaps to the pending label while the shell is pending", async () => {
      const { container } = await render(<Harness pending pendingLabel="Sending..." />);

      const button = byTestId(container, "demo-submit") as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button.textContent).toBe("Sending...");
    });

    it("still disables while pending when no pendingLabel is given", async () => {
      // CreateInquiryForm and CreateOrganizationForm never swap their label, so
      // the swap has to be optional while the disable is not.
      const { container } = await render(<Harness pending />);

      const button = byTestId(container, "demo-submit") as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button.textContent).toBe("Send");
    });

    it("lets an explicit testId beat the derived id", async () => {
      // CreateOrganizationForm's submit is `organization-create-submit` while
      // its fields derive from the bare `organization` prefix.
      const { container } = await render(
        <Harness testIdPrefix="organization" submitTestId="organization-create-submit" />,
      );

      expect(byTestId(container, "organization-create-submit").tagName).toBe("BUTTON");
      expect(queryTestId(container, "organization-submit")).toBeNull();
    });
  });

  describe("FormError", () => {
    it("renders the shell's serverError with the derived {prefix}-error testid", async () => {
      const { container } = await render(<Harness serverError="Could not submit." />);

      expect(byTestId(container, "demo-error").textContent).toBe("Could not submit.");
    });

    it("disappears once the shell's serverError clears", async () => {
      // Stated as a transition rather than a bare "renders nothing when null" so
      // a component that renders nothing at all cannot satisfy it.
      const { container, rerender } = await render(<Harness serverError="Could not submit." />);

      expect(queryTestId(container, "demo-error")).not.toBeNull();

      await rerender(<Harness serverError={null} />);

      expect(queryTestId(container, "demo-error")).toBeNull();
    });

    it("renders nothing when the shell has no serverError at all", async () => {
      const { container } = await render(<Harness />);

      expect(queryTestId(container, "demo-error")).toBeNull();
    });

    it("lets an explicit testId beat the derived id", async () => {
      // RegisterAppForm's banner is `app-register-error` while its fields derive
      // from the bare `app` prefix.
      const { container } = await render(
        <Harness testIdPrefix="app" errorTestId="app-register-error" serverError="Nope." />,
      );

      expect(byTestId(container, "app-register-error").textContent).toBe("Nope.");
      expect(queryTestId(container, "app-error")).toBeNull();
    });
  });

  describe("the shell context", () => {
    it("refuses to render a shell component outside <AppForm>", async () => {
      // The contract that lets every child read `testIdPrefix`/`pending`/
      // `serverError` without prop-threading: rendered outside the shell there is
      // no sensible fallback, so it must fail loudly rather than silently stamp
      // an `undefined-submit` testid.
      const { container } = await render(
        <ErrorProbe>
          <ContextReader />
        </ErrorProbe>,
      );

      expect(byTestId(container, "probe-message").textContent).toContain("<AppForm>");

      // React reports a boundary catch through `console.error`, which the project
      // guard fails the test over unless the spec that provoked it reads it back.
      await consumeConsoleNoise();
    });
  });
});

/** Calls the shell hook with no provider above it. */
function ContextReader() {
  const context = useAppFormContext();

  return <span data-testid="probe-value">{context.testIdPrefix}</span>;
}

/** Catches a render-time throw so the assertion can read its message. */
class ErrorProbe extends Component<{ children: ReactNode }, { message: string | null }> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { message: null };
  }

  static getDerivedStateFromError(error: unknown): { message: string } {
    return { message: error instanceof Error ? error.message : String(error) };
  }

  render(): ReactNode {
    if (this.state.message === null) {
      return this.props.children;
    }

    return <span data-testid="probe-message">{this.state.message}</span>;
  }
}
