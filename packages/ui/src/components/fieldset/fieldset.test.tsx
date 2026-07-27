import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Field } from "../field/field";
import { Input } from "../input/input";
import { Fieldset } from "./fieldset";

/*
 * Fieldset is NEW in the Base UI rebuild — there is no pre-rebuild component to
 * stay compatible with, so every assertion here is about Base UI's contract.
 *
 * Two of them are measured facts that read as surprises, and are pinned for
 * exactly that reason:
 *   - `Fieldset.Legend` renders a `<div>`, not a `<legend>`, and is tied to the
 *     fieldset through `aria-labelledby`.
 *   - `Fieldset.Root disabled` disables its controls through the NATIVE
 *     `<fieldset disabled>` attribute, so those controls get no `data-disabled`
 *     of their own — unlike `Field.Root disabled`, which publishes one.
 */

const ROOT_RECIPE = "space-y-4";
const LEGEND_RECIPE = "text-base font-medium text-foreground";

function recipeSet(recipe: string): string[] {
  return recipe.split(" ").toSorted();
}

function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();
  return element as HTMLElement;
}

describe("Fieldset", () => {
  it("renders a fieldset element carrying the root recipe and its children", async () => {
    const { container } = await render(
      <Fieldset data-testid="group">
        <span data-testid="child" />
      </Fieldset>,
    );

    const group = byTestId(container, "group");
    expect(group.tagName).toBe("FIELDSET");
    expect(classSet(group)).toEqual(recipeSet(ROOT_RECIPE));
    expect(group.querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("is its own Root, so a fork can write either name", async () => {
    expect(Fieldset.Root).toBe(Fieldset);
  });

  it("exposes exactly Base UI's Fieldset parts, under Base UI's names", async () => {
    expect(Object.keys(Fieldset).toSorted()).toEqual(["Legend", "Root"]);
  });

  it("renders the legend as a div, and names the fieldset with it", async () => {
    // Base UI trades the `<legend>` element — which cannot be laid out reliably
    // — for the same accessible name via aria-labelledby.
    const { container } = await render(
      <Fieldset data-testid="group">
        <Fieldset.Legend data-testid="legend">Contact details</Fieldset.Legend>
      </Fieldset>,
    );

    const legend = byTestId(container, "legend");
    expect(legend.tagName).toBe("DIV");
    expect(legend.textContent).toBe("Contact details");
    expect(legend.id).not.toBe("");
    expect(classSet(legend)).toEqual(recipeSet(LEGEND_RECIPE));
    expect(byTestId(container, "group").getAttribute("aria-labelledby")).toBe(legend.id);
  });

  it("lets a caller className override a recipe utility on either part", async () => {
    const { container } = await render(
      <Fieldset className="space-y-8" data-testid="group">
        <Fieldset.Legend className="text-lg" data-testid="legend">
          Contact details
        </Fieldset.Legend>
      </Fieldset>,
    );

    const group = byTestId(container, "group");
    expect(group.classList.contains("space-y-8")).toBe(true);
    expect(group.classList.contains("space-y-4")).toBe(false);

    const legend = byTestId(container, "legend");
    expect(legend.classList.contains("text-lg")).toBe(true);
    expect(legend.classList.contains("text-base")).toBe(false);
    expect(legend.classList.contains("font-medium")).toBe(true);
  });

  it("disables itself and its legend, and natively disables the controls it wraps", async () => {
    const { container } = await render(
      <Fieldset disabled data-testid="group">
        <Fieldset.Legend data-testid="legend">Contact details</Fieldset.Legend>
        <Input data-testid="input" />
      </Fieldset>,
    );

    const group = byTestId(container, "group") as HTMLFieldSetElement;
    expect(group.disabled).toBe(true);
    expect(group.getAttribute("data-disabled")).toBe("");
    expect(byTestId(container, "legend").getAttribute("data-disabled")).toBe("");

    // Measured: the native fieldset does the disabling, so the control inside
    // has no data-disabled of its own. Style it from the root's instead.
    const input = byTestId(container, "input") as HTMLInputElement;
    expect(input.hasAttribute("data-disabled")).toBe(false);
    expect(input.matches(":disabled")).toBe(true);
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<Fieldset data-testid="group" />);

    expect(byTestId(container, "group").hasAttribute("data-disabled")).toBe(false);
  });

  it("composes onto another element through the render prop", async () => {
    const { container } = await render(<Fieldset data-testid="group" render={<section />} />);

    const group = byTestId(container, "group");
    expect(group.tagName).toBe("SECTION");
    expect(classSet(group)).toEqual(recipeSet(ROOT_RECIPE));
  });

  it("groups whole Fields, each keeping its own label association", async () => {
    const { container } = await render(
      <Fieldset data-testid="group">
        <Fieldset.Legend data-testid="legend">Contact details</Fieldset.Legend>
        <Field>
          <Field.Label data-testid="email-label">Email</Field.Label>
          <Field.Control data-testid="email-control" />
        </Field>
        <Field>
          <Field.Label data-testid="phone-label">Phone</Field.Label>
          <Field.Control data-testid="phone-control" />
        </Field>
      </Fieldset>,
    );

    const emailControl = byTestId(container, "email-control");
    const phoneControl = byTestId(container, "phone-control");

    expect(emailControl.id).not.toBe(phoneControl.id);
    expect((byTestId(container, "email-label") as HTMLLabelElement).htmlFor).toBe(emailControl.id);
    expect((byTestId(container, "phone-label") as HTMLLabelElement).htmlFor).toBe(phoneControl.id);
  });
});
