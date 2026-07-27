import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";

import { Input } from "../input/input";
import { Field } from "./field";

/*
 * Follows the exemplar spec shape from button.test.tsx (Wallow-m5aq.2.1):
 * browser project, nothing mocked, every recipe asserted THROUGH the component,
 * and class assertions as an order-free SET because cn()/tailwind-merge reorders.
 *
 * COMPAT GUARANTEE (this component): the pre-rebuild `Field` was
 * `<div className="space-y-2">` with children passed through, used at 22 call
 * sites. That row must render identically, which is why the root's class set is
 * pinned as an exact match rather than a "contains".
 *
 * Everything else here is what a real Field.Root adds and a bare div could not:
 * the label/control/description/error association, and the field state
 * published to every part as `data-*` attributes. Those attributes were
 * measured against the installed @base-ui/react 1.6.0 rather than read from the
 * docs — including the two that would otherwise be guessed wrong:
 *   - `Field.Error` renders NOTHING at all until its `match` is satisfied, even
 *     when the root is `invalid`.
 *   - a `Field.Root` with `invalid` does not by itself produce an error MESSAGE;
 *     a message needs `validate` (or `match`).
 */

/** The pre-rebuild row recipe, verbatim from the Field this replaces. */
const ROOT_RECIPE = "space-y-2";

/** The pre-rebuild label recipe plus the disabled state a Field makes possible. */
const LABEL_RECIPE = "text-sm font-medium text-foreground data-[disabled]:opacity-50";

/**
 * The control's recipe: the standalone Input's class set (Base UI's Input IS
 * Field.Control underneath, so the two must render alike) plus the invalid
 * treatment that only exists inside a Field — the one Wallow-m5aq.2.2 deferred
 * to this task because it could not be tested from there.
 */
const INPUT_RECIPE =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50";
const CONTROL_INVALID_UTILITY = "data-[invalid]:border-destructive";
const CONTROL_RECIPE = `${INPUT_RECIPE} ${CONTROL_INVALID_UTILITY}`;

const DESCRIPTION_RECIPE = "text-sm text-muted-foreground";
const ERROR_RECIPE = "text-sm text-destructive";
const ITEM_RECIPE = "flex items-center gap-2";

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

describe("Field", () => {
  describe("compat with the pre-rebuild row", () => {
    it("renders the space-y-2 row wrapper around its children", async () => {
      const { container } = await render(
        <Field>
          <span data-testid="child" />
        </Field>,
      );

      const row = container.firstElementChild as HTMLElement | null;
      expect(row).not.toBeNull();
      expect((row as HTMLElement).tagName).toBe("DIV");
      expect(classSet(row as HTMLElement)).toEqual(recipeSet(ROOT_RECIPE));
      expect((row as HTMLElement).querySelector('[data-testid="child"]')).not.toBeNull();
    });

    it("passes through an app-owned data-testid", async () => {
      const { container } = await render(<Field data-testid="login-email-field" />);

      expect(container.querySelector('[data-testid="login-email-field"]')).not.toBeNull();
    });

    it("lets a caller className override the row recipe", async () => {
      // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
      // rather than appended after. The pre-rebuild Field hard-coded its
      // className and dropped the caller's entirely, so this is also the fix for
      // a real footgun.
      const { container } = await render(<Field className="space-y-4" data-testid="row" />);

      const row = byTestId(container, "row");
      expect(row.classList.contains("space-y-4")).toBe(true);
      expect(row.classList.contains("space-y-2")).toBe(false);
    });

    it("composes the row onto another element through the render prop", async () => {
      const { container } = await render(<Field data-testid="row" render={<section />} />);

      const row = byTestId(container, "row");
      expect(row.tagName).toBe("SECTION");
      expect(classSet(row)).toEqual(recipeSet(ROOT_RECIPE));
    });
  });

  describe("anatomy", () => {
    it("is its own Root, so a fork can write either name", async () => {
      expect(Field.Root).toBe(Field);
    });

    it("exposes exactly Base UI's Field parts, under Base UI's names", async () => {
      // Pinned as an exact set: the catalog's rule is that a multi-part
      // component's namespace mirrors Base UI's part list 1:1, so both a
      // dropped part and an invented one are failures.
      expect(Object.keys(Field).toSorted()).toEqual([
        "Control",
        "Description",
        "Error",
        "Item",
        "Label",
        "Root",
        "Validity",
      ]);
    });

    it("renders each part as the element Base UI documents", async () => {
      const { container } = await render(
        <Field>
          <Field.Label data-testid="label">Email</Field.Label>
          <Field.Control data-testid="control" />
          <Field.Description data-testid="description">We never share it.</Field.Description>
        </Field>,
      );

      expect(byTestId(container, "label").tagName).toBe("LABEL");
      expect(byTestId(container, "control").tagName).toBe("INPUT");
      expect(byTestId(container, "description").tagName).toBe("P");
    });

    it("renders each part's recipe", async () => {
      const { container } = await render(
        <Field>
          <Field.Label data-testid="label">Email</Field.Label>
          <Field.Control data-testid="control" />
          <Field.Description data-testid="description">We never share it.</Field.Description>
        </Field>,
      );

      expect(classSet(byTestId(container, "label"))).toEqual(recipeSet(LABEL_RECIPE));
      expect(classSet(byTestId(container, "control"))).toEqual(recipeSet(CONTROL_RECIPE));
      expect(classSet(byTestId(container, "description"))).toEqual(recipeSet(DESCRIPTION_RECIPE));
    });

    it("styles the control exactly like the standalone Input, plus the invalid treatment", async () => {
      // Base UI's Input IS Field.Control, so a caller may use either inside a
      // Field and must not be able to tell which they picked. The only
      // difference is the utility that needs the Field context to fire.
      const { container } = await render(
        <Field>
          <Field.Control data-testid="control" />
        </Field>,
      );

      const control = byTestId(container, "control");
      for (const utility of INPUT_RECIPE.split(" ")) {
        expect(control.classList.contains(utility), utility).toBe(true);
      }

      expect(control.classList.contains(CONTROL_INVALID_UTILITY)).toBe(true);
    });

    it("lets a caller className override a part's recipe utility", async () => {
      const { container } = await render(
        <Field>
          <Field.Control className="bg-accent" data-testid="control" />
        </Field>,
      );

      const control = byTestId(container, "control");
      expect(control.classList.contains("bg-accent")).toBe(true);
      expect(control.classList.contains("bg-background")).toBe(false);
      expect(control.classList.contains("rounded-md")).toBe(true);
    });
  });

  describe("association", () => {
    it("points the label at the control with no htmlFor from the caller", async () => {
      // The upgrade over the pre-rebuild row: callers no longer hand-maintain an
      // htmlFor/id pair, which is exactly the kind of thing that silently rots.
      const { container } = await render(
        <Field>
          <Field.Label data-testid="label">Email</Field.Label>
          <Field.Control data-testid="control" />
        </Field>,
      );

      const label = byTestId(container, "label") as HTMLLabelElement;
      const control = byTestId(container, "control");

      expect(control.id).not.toBe("");
      expect(label.htmlFor).toBe(control.id);
    });

    it("keeps an explicit htmlFor from the caller", async () => {
      // All 12 in-repo Label call sites pass htmlFor against a hand-written id,
      // so the caller's value has to win over the generated one.
      const { container } = await render(
        <Field>
          <Field.Label htmlFor="email" data-testid="label">
            Email
          </Field.Label>
          <Field.Control id="email" data-testid="control" />
        </Field>,
      );

      expect((byTestId(container, "label") as HTMLLabelElement).htmlFor).toBe("email");
      expect(byTestId(container, "control").id).toBe("email");
    });

    it("describes the control with the description", async () => {
      const { container } = await render(
        <Field>
          <Field.Control data-testid="control" />
          <Field.Description data-testid="description">We never share it.</Field.Description>
        </Field>,
      );

      const description = byTestId(container, "description");
      expect(description.id).not.toBe("");
      expect(byTestId(container, "control").getAttribute("aria-describedby")).toBe(description.id);
    });

    it("groups a control with its own label inside a Field.Item", async () => {
      const { container } = await render(
        <Field>
          <Field.Item data-testid="item">
            <Field.Label data-testid="item-label">Remember me</Field.Label>
            <Field.Control data-testid="item-control" />
          </Field.Item>
        </Field>,
      );

      const item = byTestId(container, "item");
      expect(item.tagName).toBe("DIV");
      expect(classSet(item)).toEqual(recipeSet(ITEM_RECIPE));
      expect((byTestId(container, "item-label") as HTMLLabelElement).htmlFor).toBe(
        byTestId(container, "item-control").id,
      );
    });
  });

  describe("state", () => {
    it("publishes the invalid state to the root, the label and the control", async () => {
      const { container } = await render(
        <Field invalid data-testid="row">
          <Field.Label data-testid="label">Email</Field.Label>
          <Field.Control data-testid="control" />
        </Field>,
      );

      expect(byTestId(container, "row").getAttribute("data-invalid")).toBe("");
      expect(byTestId(container, "label").getAttribute("data-invalid")).toBe("");

      const control = byTestId(container, "control");
      expect(control.getAttribute("data-invalid")).toBe("");
      expect(control.getAttribute("aria-invalid")).toBe("true");
    });

    it("publishes the disabled state and disables the control", async () => {
      const { container } = await render(
        <Field disabled data-testid="row">
          <Field.Label data-testid="label">Email</Field.Label>
          <Field.Control data-testid="control" />
        </Field>,
      );

      expect(byTestId(container, "row").getAttribute("data-disabled")).toBe("");
      expect(byTestId(container, "label").getAttribute("data-disabled")).toBe("");

      const control = byTestId(container, "control") as HTMLInputElement;
      expect(control.getAttribute("data-disabled")).toBe("");
      expect(control.disabled).toBe(true);
    });

    it("carries no state attributes on a fresh, untouched field", async () => {
      const { container } = await render(
        <Field data-testid="row">
          <Field.Control data-testid="control" />
        </Field>,
      );

      const row = byTestId(container, "row");
      for (const attribute of ["data-invalid", "data-disabled", "data-dirty", "data-touched"]) {
        expect(row.hasAttribute(attribute), attribute).toBe(false);
      }
    });

    it("tracks dirty, filled and touched as the user types and leaves", async () => {
      const { container } = await render(
        <Field data-testid="row">
          <Field.Control data-testid="control" />
        </Field>,
      );

      const control = byTestId(container, "control") as HTMLInputElement;
      await userEvent.fill(control, "ada@example.com");
      expect(control.getAttribute("data-focused")).toBe("");
      expect(control.getAttribute("data-filled")).toBe("");

      await userEvent.click(document.body);
      expect(control.getAttribute("data-touched")).toBe("");
      expect(byTestId(container, "row").getAttribute("data-dirty")).toBe("");
      expect(byTestId(container, "row").getAttribute("data-touched")).toBe("");
    });

    it("lights up the field state on a plain <Input> placed inside it", async () => {
      // The claim Wallow-m5aq.2.2 left for this task to prove: because Base UI's
      // Input IS Field.Control, the existing <Input> gains the whole state
      // contract from the Field context with no change to input.tsx. That is
      // what makes the 22 <Field><Label/><Input/></Field> call sites an upgrade
      // rather than a rewrite.
      const { container } = await render(
        <Field invalid>
          <Field.Label data-testid="label">Email</Field.Label>
          <Input data-testid="input" />
        </Field>,
      );

      const input = byTestId(container, "input");
      expect(input.getAttribute("data-invalid")).toBe("");
      expect(input.getAttribute("aria-invalid")).toBe("true");
      expect((byTestId(container, "label") as HTMLLabelElement).htmlFor).toBe(input.id);
    });
  });

  describe("validation", () => {
    it("renders nothing for an error whose match is not satisfied", async () => {
      // Measured, and the single easiest thing to get wrong here: an `invalid`
      // root is NOT enough to show a message.
      const { container } = await render(
        <Field invalid>
          <Field.Control data-testid="control" />
          <Field.Error data-testid="error">Enter a valid email</Field.Error>
        </Field>,
      );

      expect(container.querySelector('[data-testid="error"]')).toBeNull();
    });

    it("renders an always-matched error with its recipe, and describes the control with it", async () => {
      const { container } = await render(
        <Field invalid>
          <Field.Control data-testid="control" />
          <Field.Error match data-testid="error">
            Enter a valid email
          </Field.Error>
        </Field>,
      );

      const error = byTestId(container, "error");
      expect(error.tagName).toBe("DIV");
      expect(error.textContent).toBe("Enter a valid email");
      expect(classSet(error)).toEqual(recipeSet(ERROR_RECIPE));
      expect(byTestId(container, "control").getAttribute("aria-describedby")).toBe(error.id);
    });

    it("shows the validate callback's message once the control is blurred", async () => {
      const { container } = await render(
        <Field
          validationMode="onBlur"
          validate={(value) => (String(value).includes("@") ? null : "Enter a valid email")}
          data-testid="row"
        >
          <Field.Control data-testid="control" />
          <Field.Error data-testid="error" />
        </Field>,
      );

      expect(container.querySelector('[data-testid="error"]')).toBeNull();

      const control = byTestId(container, "control") as HTMLInputElement;
      await userEvent.fill(control, "nope");
      await userEvent.click(document.body);

      expect(byTestId(container, "error").textContent).toBe("Enter a valid email");
      expect(byTestId(container, "row").getAttribute("data-invalid")).toBe("");
      expect(control.getAttribute("aria-invalid")).toBe("true");
    });

    it("hands the field's validity data to the Validity render prop", async () => {
      const { container } = await render(
        <Field invalid>
          <Field.Control data-testid="control" />
          <Field.Validity>
            {(validity) => <span data-testid="validity">{String(validity.validity.valid)}</span>}
          </Field.Validity>
        </Field>,
      );

      expect(byTestId(container, "validity").textContent).toBe("false");
    });
  });
});
