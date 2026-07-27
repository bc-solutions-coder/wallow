import { render } from "@bc-solutions-coder/testing/render";
import { Component, type ErrorInfo, type ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { Field } from "../field/field";
import { Label } from "./label";

/*
 * `Label` is the compat name for `Field.Label` — Base UI ships no standalone
 * `label` part, because associating itself with a field's control is the whole
 * job of a label. These specs cover both halves of that: the three assertions
 * the pre-rebuild Label already had to satisfy (recipe, htmlFor, data-testid),
 * now made inside a `<Field>`, and the one deliberate behaviour change the
 * rebuild introduces — a Label outside a Field throws.
 *
 * All 12 in-repo `<Label>` call sites already sit inside a `<Field>`, so that
 * change moves none of them; it is pinned here so a future change has to
 * acknowledge it rather than discover it.
 */

/** The pre-rebuild recipe, verbatim, plus the disabled state a Field enables. */
const LEGACY_RECIPE = "text-sm font-medium text-foreground";
const STATE_DISABLED_UTILITY = "data-[disabled]:opacity-50";

function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function expectedClasses(): string[] {
  return [...LEGACY_RECIPE.split(" "), STATE_DISABLED_UTILITY].toSorted();
}

function onlyLabel(container: HTMLElement): HTMLLabelElement {
  const label = container.querySelector("label");
  expect(label).not.toBeNull();
  return label as HTMLLabelElement;
}

/** Catches the render-time throw so the assertion is about the message. */
class Boundary extends Component<{ readonly children: ReactNode }, { readonly message: string }> {
  public constructor(props: { readonly children: ReactNode }) {
    super(props);
    this.state = { message: "" };
  }

  public static getDerivedStateFromError(error: Error): { readonly message: string } {
    return { message: error.message };
  }

  public componentDidCatch(_error: Error, _info: ErrorInfo): void {
    // Swallowed on purpose: the throw is the behaviour under test.
  }

  public override render(): ReactNode {
    if (this.state.message !== "") {
      return <p data-testid="boundary">{this.state.message}</p>;
    }

    return this.props.children;
  }
}

describe("Label", () => {
  it("renders the pre-rebuild recipe with its children", async () => {
    const { container } = await render(
      <Field>
        <Label>Email</Label>
      </Field>,
    );

    const label = onlyLabel(container);
    expect(classSet(label)).toEqual(expectedClasses());
    expect(label.textContent).toBe("Email");
  });

  it("forwards htmlFor onto the label's for attribute", async () => {
    const { container } = await render(
      <Field>
        <Label htmlFor="email">Email</Label>
      </Field>,
    );

    expect(onlyLabel(container).htmlFor).toBe("email");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <Field>
        <Label data-testid="login-email-label">Email</Label>
      </Field>,
    );

    expect(container.querySelector('[data-testid="login-email-label"]')).not.toBeNull();
  });

  it("is the very same component as Field.Label, so the two cannot drift", async () => {
    expect(Label).toBe(Field.Label);
  });

  it("associates itself with the field's control when the caller gives no htmlFor", async () => {
    // What the alias buys the 12 call sites: htmlFor becomes optional, and the
    // hand-maintained htmlFor/id pair stops being a thing that can rot.
    const { container } = await render(
      <Field>
        <Label>Email</Label>
        <Field.Control data-testid="control" />
      </Field>,
    );

    const control = container.querySelector('[data-testid="control"]') as HTMLInputElement | null;
    expect(control).not.toBeNull();
    expect((control as HTMLInputElement).id).not.toBe("");
    expect(onlyLabel(container).htmlFor).toBe((control as HTMLInputElement).id);
  });

  it("lets a caller className override a recipe utility", async () => {
    const { container } = await render(
      <Field>
        <Label className="text-base">Email</Label>
      </Field>,
    );

    const label = onlyLabel(container);
    expect(label.classList.contains("text-base")).toBe(true);
    expect(label.classList.contains("text-sm")).toBe(false);
    expect(label.classList.contains("font-medium")).toBe(true);
  });

  it("carries the field's disabled state as a data attribute", async () => {
    // What `data-[disabled]:opacity-50` hooks off. A bare <label> has no
    // equivalent — nothing about a disabled input reaches its label in HTML.
    const { container } = await render(
      <Field disabled>
        <Label>Email</Label>
        <Field.Control />
      </Field>,
    );

    expect(onlyLabel(container).getAttribute("data-disabled")).toBe("");
  });

  it("throws outside a Field, because a label is a member of the field anatomy", async () => {
    // The one deliberate behaviour change from the pre-rebuild <label>. Pinned
    // so it stays a decision rather than a surprise.
    const { container } = await render(
      <Boundary>
        <Label>Email</Label>
      </Boundary>,
    );

    const boundary = container.querySelector('[data-testid="boundary"]');
    expect(boundary).not.toBeNull();
    expect((boundary as HTMLElement).textContent).toContain("FieldRootContext is missing");
  });
});
