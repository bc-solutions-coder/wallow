import { render } from "@bc-solutions-coder/testing/render";
import { useState } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";

import { Input } from "./input";

/*
 * Follows the exemplar spec shape established by button.test.tsx (Wallow-m5aq.2.1):
 * browser project, nothing mocked, the recipe asserted THROUGH the component, and
 * class assertions as an order-free SET because cn()/tailwind-merge may reorder.
 *
 * COMPAT GUARANTEE (this component): the pre-rebuild Input was a hand-rolled
 * string-append rendering a bare <input>, used at 23 call sites across
 * wallow-auth and wallow-web. Its class set must survive the rebuild, with ONE
 * deliberate addition pinned by its own test below: `data-[disabled]:opacity-50`,
 * the state treatment Base UI's `data-disabled` attribute makes possible and the
 * pre-rebuild input had no equivalent of.
 *
 * Which state data-attributes are asserted here was settled empirically against
 * @base-ui/react 1.6.0 rather than from the docs: Base UI's Input is Field.Control
 * underneath, and OUTSIDE a Field.Root the only state attribute it renders is
 * `data-disabled`. `data-valid`/`data-invalid`/`data-touched`/`data-dirty`/
 * `data-filled`/`data-focused` all require the Field context and stay untested
 * here on purpose — they belong to the Field task, which owns that subpath.
 */

/** The pre-rebuild recipe, verbatim from the Input this replaces. */
const LEGACY_RECIPE =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground";

/** The one utility the rebuild adds on top of the legacy recipe. */
const STATE_DISABLED_UTILITY = "data-[disabled]:opacity-50";

/** Every utility the input renders. */
const BASE_CLASSES = [...LEGACY_RECIPE.split(" "), STATE_DISABLED_UTILITY];

/** The full expected class set, order-free. */
function expectedClasses(): string[] {
  return [...BASE_CLASSES].toSorted();
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlyInput(container: HTMLElement): HTMLInputElement {
  const input = container.querySelector("input");
  expect(input).not.toBeNull();
  return input as HTMLInputElement;
}

/** The controlled value/onChange pairing every real call site uses. */
function ControlledInput(): ReturnType<typeof Input> {
  const [value, setValue] = useState("");

  return (
    <Input
      value={value}
      onChange={(event) => {
        setValue(event.target.value);
      }}
      data-testid="controlled-input"
    />
  );
}

describe("Input", () => {
  it("renders the recipe by default", async () => {
    const { container } = await render(<Input />);

    expect(classSet(onlyInput(container))).toEqual(expectedClasses());
  });

  it("keeps every class of the pre-rebuild recipe, adding only the disabled-state utility", async () => {
    // The compat guarantee, stated as its own test so a future change to the
    // recipe has to acknowledge which of the 23 measured call sites it moves.
    const { container } = await render(<Input />);

    const input = onlyInput(container);
    for (const legacy of LEGACY_RECIPE.split(" ")) {
      expect(input.classList.contains(legacy), legacy).toBe(true);
    }

    expect(input.classList.contains(STATE_DISABLED_UTILITY)).toBe(true);
  });

  it("forwards native input attributes (id, type, placeholder, name, required, autoComplete)", async () => {
    const { container } = await render(
      <Input
        id="email"
        type="email"
        placeholder="name@example.com"
        name="email"
        required
        autoComplete="email"
      />,
    );

    const input = onlyInput(container);
    expect(input.id).toBe("email");
    expect(input.type).toBe("email");
    expect(input.placeholder).toBe("name@example.com");
    expect(input.name).toBe("email");
    expect(input.required).toBe(true);
    expect(input.autocomplete).toBe("email");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Input data-testid="login-email" />);

    expect(container.querySelector('[data-testid="login-email"]')).not.toBeNull();
  });

  it("exposes the disabled state as a data attribute", async () => {
    // Base UI's state contract, and what `data-[disabled]:opacity-50` hooks off.
    // A hand-rolled <input disabled> renders no such attribute.
    const { container } = await render(<Input disabled />);

    const input = onlyInput(container);
    expect(input.disabled).toBe(true);
    expect(input.getAttribute("data-disabled")).toBe("");
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<Input />);

    expect(onlyInput(container).hasAttribute("data-disabled")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both `bg-background` and `bg-accent`
    // on the element and fails here.
    const { container } = await render(<Input className="bg-accent" />);

    const input = onlyInput(container);
    expect(input.classList.contains("bg-accent")).toBe(true);
    expect(input.classList.contains("bg-background")).toBe(false);
    expect(input.classList.contains("rounded-md")).toBe(true);
    expect(input.classList.contains("text-foreground")).toBe(true);
  });

  it("composes onto another element through the render prop", async () => {
    // The recipe has to travel to whatever element the caller substitutes.
    const { container } = await render(<Input render={<input type="password" />} />);

    const input = onlyInput(container);
    expect(input.type).toBe("password");
    expect(classSet(input)).toEqual(expectedClasses());
  });

  it("drives a caller-controlled value through the caller's onChange", async () => {
    // The shape all 23 call sites use: `value={state}` plus an `onChange` that
    // reads `event.target.value`. Base UI merges its own change handler over the
    // caller's, so this pins that the caller's still runs and the value lands.
    const { container } = await render(<ControlledInput />);

    const input = onlyInput(container);
    await userEvent.fill(input, "name@example.com");

    expect(input.value).toBe("name@example.com");
  });

  it("stamps a generated id when the caller supplies none", async () => {
    // A deliberate delta from the pre-rebuild bare <input>, which had no id at
    // all: Base UI always mints one so Field.Label can point `htmlFor` at it.
    const { container } = await render(<Input />);

    expect(onlyInput(container).id).not.toBe("");
  });
});
