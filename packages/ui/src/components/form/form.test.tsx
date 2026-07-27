import { render } from "@bc-solutions-coder/testing/render";
import type { RefObject } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { Field } from "../field/field";
import { Form, type FormActions, type FormSubmitEventDetails } from "./form";

/*
 * Follows the exemplar spec shape from button.test.tsx (Wallow-m5aq.2.1):
 * browser project, nothing mocked, the recipe asserted THROUGH the component,
 * and class assertions as an order-free SET because cn()/tailwind-merge is free
 * to reorder.
 *
 * Form is the wiring component of the wave, so most of what is pinned below is
 * the CONTRACT BETWEEN Form AND Field rather than markup. Every behaviour was
 * measured against the installed @base-ui/react 1.6.0 with a throwaway probe
 * before it was asserted, including the four a reader would otherwise guess
 * wrong:
 *
 *   - a `Field.Error` with NO `match` prop DOES render a message that arrived
 *     through the form's `errors` prop. Wallow-m5aq.2.3 measured that an
 *     `invalid` field alone renders no element at all; a form error is the
 *     exception, and it is the whole point of this task.
 *   - a server error also makes the field invalid, so it BLOCKS the next submit
 *     until the user edits that field (which clears the error).
 *   - `onSubmit` still fires, and the native default is prevented only when
 *     `onFormSubmit` is supplied. A spec that omits `onFormSubmit` and clicks
 *     submit navigates the browser-mode iframe, so every submit spec here
 *     supplies one.
 *   - an `errors` value that is an ARRAY renders as a `<ul>` of `<li>`, not as
 *     joined text.
 */

/** The stacking rhythm every hand-written `<form>` in wallow-auth already uses. */
const FORM_RECIPE = "space-y-4";

/** A recipe as an order-free set, to compare against a rendered classList. */
function recipeSet(recipe: string): string[] {
  return recipe.split(" ").toSorted();
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();
  return element as HTMLElement;
}

function queryTestId(container: HTMLElement, id: string): HTMLElement | null {
  return container.querySelector(`[data-testid="${id}"]`);
}

type SubmitSpy = (values: Record<string, unknown>, details: FormSubmitEventDetails) => void;

describe("Form", () => {
  describe("the form element", () => {
    it("renders a native form carrying the recipe", async () => {
      const { container } = await render(<Form data-testid="form" />);

      const form = byTestId(container, "form");
      expect(form.tagName).toBe("FORM");
      expect(classSet(form)).toEqual(recipeSet(FORM_RECIPE));
    });

    it("turns off the browser's own validation so Base UI owns the messages", async () => {
      // Without noValidate the browser shows its native bubble and never lets
      // Field.Error render, so this attribute is load-bearing rather than
      // cosmetic.
      const { container } = await render(<Form data-testid="form" />);

      expect((byTestId(container, "form") as HTMLFormElement).noValidate).toBe(true);
    });

    it("passes through native form attributes and an app-owned data-testid", async () => {
      const { container } = await render(
        <Form data-testid="login-form" aria-label="Sign in" id="login" />,
      );

      const form = byTestId(container, "login-form");
      expect(form.getAttribute("aria-label")).toBe("Sign in");
      expect(form.id).toBe("login");
    });

    it("lets a caller className override the recipe", async () => {
      // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
      // rather than appended after, which a string-append implementation fails.
      const { container } = await render(<Form className="space-y-6" data-testid="form" />);

      const form = byTestId(container, "form");
      expect(form.classList.contains("space-y-6")).toBe(true);
      expect(form.classList.contains("space-y-4")).toBe(false);
    });

    it("composes onto a caller's element through the render prop", async () => {
      const { container } = await render(
        <Form data-testid="form" render={<form data-render="composed" />} />,
      );

      const form = byTestId(container, "form");
      expect(form.tagName).toBe("FORM");
      expect(form.getAttribute("data-render")).toBe("composed");
      expect(classSet(form)).toEqual(recipeSet(FORM_RECIPE));
    });

    it("forwards a ref to the form element", async () => {
      const ref: RefObject<HTMLFormElement | null> = { current: null };
      await render(<Form ref={ref} data-testid="form" />);

      expect(ref.current?.tagName).toBe("FORM");
    });

    it("renders its children", async () => {
      const { container } = await render(
        <Form>
          <span data-testid="child" />
        </Form>,
      );

      expect(queryTestId(container, "child")).not.toBeNull();
    });
  });

  describe("submission", () => {
    it("hands onFormSubmit the field values keyed by each field's name", async () => {
      const onFormSubmit = vi.fn<SubmitSpy>();
      const { container } = await render(
        <Form onFormSubmit={onFormSubmit}>
          <Field name="email">
            <Field.Control defaultValue="ada@example.com" />
          </Field>
          <Field name="password">
            <Field.Control type="password" defaultValue="hunter2" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      expect(onFormSubmit).toHaveBeenCalledTimes(1);
      expect(onFormSubmit.mock.calls[0]?.[0]).toEqual({
        email: "ada@example.com",
        password: "hunter2",
      });
    });

    it("prevents the native submit so the page never navigates", async () => {
      // This is why every call site can drop its own preventDefault/stopPropagation
      // pair: six hand-written <form>s across the apps open with exactly that.
      const onFormSubmit = vi.fn<SubmitSpy>();
      const { container } = await render(
        <Form onFormSubmit={onFormSubmit}>
          <Field name="email">
            <Field.Control defaultValue="ada@example.com" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      const details = onFormSubmit.mock.calls[0]?.[1];
      expect(details?.reason).toBe("none");
      expect(details?.event.type).toBe("submit");
      expect(details?.event.defaultPrevented).toBe(true);
    });

    it("still calls a caller's own onSubmit handler", async () => {
      const onSubmit = vi.fn();
      const { container } = await render(
        <Form onFormSubmit={vi.fn<SubmitSpy>()} onSubmit={onSubmit}>
          <Field name="email">
            <Field.Control defaultValue="ada@example.com" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    it("blocks the submit, shows the message and focuses the first invalid field", async () => {
      const onFormSubmit = vi.fn<SubmitSpy>();
      const { container } = await render(
        <Form onFormSubmit={onFormSubmit}>
          <Field name="email" validate={(value) => (String(value) ? null : "Enter your email")}>
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      expect(onFormSubmit).not.toHaveBeenCalled();
      expect(byTestId(container, "email-error").textContent).toBe("Enter your email");
      expect(document.activeElement).toBe(byTestId(container, "email"));
    });

    it("blocks the submit on a native constraint and renders the browser's message", async () => {
      // The exact wording is Chromium's, so only its presence is pinned — what
      // matters is that a native constraint routes into Field.Error like a
      // validate() message does, instead of the browser bubble.
      const onFormSubmit = vi.fn<SubmitSpy>();
      const { container } = await render(
        <Form onFormSubmit={onFormSubmit}>
          <Field name="email">
            <Field.Control data-testid="email" required />
            <Field.Error data-testid="email-error" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      expect(onFormSubmit).not.toHaveBeenCalled();
      expect(byTestId(container, "email-error").textContent).not.toBe("");
    });
  });

  describe("server errors wired into Field.Error", () => {
    it("renders an error from the errors prop on the field of that name, with no match", async () => {
      // The contract this task exists for. Wallow-m5aq.2.3 pinned that an
      // `invalid` field with a bare <Field.Error> renders NO element; routed
      // through the form's `errors`, the very same markup renders the message.
      const { container } = await render(
        <Form errors={{ email: "Email already registered" }}>
          <Field name="email">
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      expect(byTestId(container, "email-error").textContent).toBe("Email already registered");
    });

    it("marks that field invalid and describes its control with the message", async () => {
      const { container } = await render(
        <Form errors={{ email: "Email already registered" }}>
          <Field name="email" data-testid="email-row">
            <Field.Label data-testid="email-label">Email</Field.Label>
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      const control = byTestId(container, "email");
      const error = byTestId(container, "email-error");
      expect(byTestId(container, "email-row").getAttribute("data-invalid")).toBe("");
      expect(byTestId(container, "email-label").getAttribute("data-invalid")).toBe("");
      expect(control.getAttribute("data-invalid")).toBe("");
      expect(control.getAttribute("aria-invalid")).toBe("true");
      expect(control.getAttribute("aria-describedby")).toBe(error.id);
    });

    it("leaves sibling fields untouched", async () => {
      const { container } = await render(
        <Form errors={{ email: "Email already registered" }}>
          <Field name="email">
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
          <Field name="password">
            <Field.Control data-testid="password" />
            <Field.Error data-testid="password-error" />
          </Field>
        </Form>,
      );

      expect(queryTestId(container, "password-error")).toBeNull();
      expect(byTestId(container, "password").getAttribute("data-invalid")).toBeNull();
    });

    it("renders a list when a field has several errors", async () => {
      const { container } = await render(
        <Form errors={{ password: ["Too short", "Needs a digit"] }}>
          <Field name="password">
            <Field.Control data-testid="password" />
            <Field.Error data-testid="password-error" />
          </Field>
        </Form>,
      );

      const messages = [...byTestId(container, "password-error").querySelectorAll("li")];
      expect(messages.map((item) => item.textContent)).toEqual(["Too short", "Needs a digit"]);
    });

    it("clears the error as soon as the user edits that field", async () => {
      const { container } = await render(
        <Form errors={{ email: "Email already registered" }}>
          <Field name="email">
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      await userEvent.fill(byTestId(container, "email") as HTMLInputElement, "ada@example.com");

      await expect.poll(() => queryTestId(container, "email-error")).toBeNull();
    });

    it("picks up errors handed in after the first render", async () => {
      // The realistic shape: the errors object arrives from a mutation's
      // response, so it changes on a later render rather than the first.
      const { container, rerender } = await render(
        <Form errors={{}}>
          <Field name="email">
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      expect(queryTestId(container, "email-error")).toBeNull();

      await rerender(
        <Form errors={{ email: "Email already registered" }}>
          <Field name="email">
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      expect(byTestId(container, "email-error").textContent).toBe("Email already registered");
    });

    it("blocks a resubmit while a server error is still showing", async () => {
      // Measured, and worth knowing before it is met in an app: a server error
      // leaves the field invalid, so hammering submit does NOT re-post. The user
      // has to change the field, which is also what clears the error.
      const onFormSubmit = vi.fn<SubmitSpy>();
      const { container } = await render(
        <Form errors={{ email: "Email already registered" }} onFormSubmit={onFormSubmit}>
          <Field name="email">
            <Field.Control data-testid="email" defaultValue="ada@example.com" />
            <Field.Error data-testid="email-error" />
          </Field>
          <button type="submit" data-testid="submit">
            Sign in
          </button>
        </Form>,
      );

      await userEvent.click(byTestId(container, "submit"));

      expect(onFormSubmit).not.toHaveBeenCalled();
      expect(byTestId(container, "email-error").textContent).toBe("Email already registered");
    });
  });

  describe("validation mode", () => {
    it("validates every field as the user types when set to onChange", async () => {
      const { container } = await render(
        <Form validationMode="onChange">
          <Field
            name="email"
            validate={(value) => (String(value).includes("@") ? null : "Enter a valid email")}
          >
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      await userEvent.fill(byTestId(container, "email") as HTMLInputElement, "nope");

      expect(byTestId(container, "email-error").textContent).toBe("Enter a valid email");
    });

    it("waits for the control to lose focus when set to onBlur", async () => {
      const { container } = await render(
        <Form validationMode="onBlur">
          <Field
            name="email"
            validate={(value) => (String(value).includes("@") ? null : "Enter a valid email")}
          >
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      await userEvent.fill(byTestId(container, "email") as HTMLInputElement, "nope");
      expect(queryTestId(container, "email-error")).toBeNull();

      await userEvent.click(document.body);
      expect(byTestId(container, "email-error").textContent).toBe("Enter a valid email");
    });

    it("lets a field's own validationMode win over the form's", async () => {
      const { container } = await render(
        <Form validationMode="onSubmit">
          <Field
            name="email"
            validationMode="onChange"
            validate={(value) => (String(value).includes("@") ? null : "Enter a valid email")}
          >
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      await userEvent.fill(byTestId(container, "email") as HTMLInputElement, "nope");

      expect(byTestId(container, "email-error").textContent).toBe("Enter a valid email");
    });
  });

  describe("imperative actions", () => {
    it("validates every field on demand through actionsRef", async () => {
      const actionsRef: RefObject<FormActions | null> = { current: null };
      const { container } = await render(
        <Form actionsRef={actionsRef}>
          <Field name="email" validate={() => "Enter a valid email"}>
            <Field.Control data-testid="email" />
            <Field.Error data-testid="email-error" />
          </Field>
        </Form>,
      );

      expect(queryTestId(container, "email-error")).toBeNull();

      actionsRef.current?.validate();

      await expect
        .poll(() => queryTestId(container, "email-error")?.textContent)
        .toBe("Enter a valid email");
    });
  });
});
