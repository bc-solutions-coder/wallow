import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { Button } from "./button";

/*
 * EXEMPLAR SPEC (Wallow-m5aq.2.1). Every component task in Waves 1-3 copies the
 * shape of this file, so the shape is part of the deliverable:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. `*.test.tsx` under src/ is collected there automatically; no
 *      environment pragma, and never jsdom/happy-dom (.claude/rules/TESTING.md).
 *   2. NOTHING is mocked. packages/ui specs render the actual Base UI part and
 *      read the actual rendered attributes.
 *   3. The recipe is asserted THROUGH the component, never by importing
 *      `buttonRecipe` and inspecting its return value. A recipe unit test would
 *      pass while the component forgot to apply it.
 *   4. Class assertions are an ORDER-FREE SET (`classSet` below), because
 *      `cn()`/tailwind-merge is free to reorder. The exact-string assertion the
 *      pre-rebuild spec used cannot survive that, so it is restated here as set
 *      equality — which is strictly stronger than the per-class `contains`
 *      checks it replaces: a stray extra utility fails too.
 *   5. Beyond the recipe, the tests cover the behavioural edges Base UI adds and
 *      a hand-rolled element does not: state data-attributes (`data-disabled`),
 *      caller-className override (proves `cn()` is wired, not string append),
 *      and the `render` prop that composes the component onto another element.
 *
 * COMPAT GUARANTEE (this component only): the pre-rebuild Button was a
 * hand-rolled string-append with a measured recipe used 11x across wallow-auth.
 * Its class set must survive the rebuild, with ONE deliberate delta pinned by
 * its own test below: `disabled:opacity-50` becomes `data-[disabled]:opacity-50`,
 * because Base UI drives state through data-attributes and the `:disabled`
 * pseudo-class does not exist on a `render`-prop anchor.
 */

/** The pre-rebuild recipe, verbatim from the Button this replaces. */
const LEGACY_PRIMARY_RECIPE =
  "w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50";

/** The one legacy utility the rebuild is allowed to drop, and its replacement. */
const LEGACY_DISABLED_UTILITY = "disabled:opacity-50";
const STATE_DISABLED_UTILITY = "data-[disabled]:opacity-50";

/** Utilities every variant renders. */
const BASE_CLASSES = [
  "inline-flex",
  "w-full",
  "items-center",
  "justify-center",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  STATE_DISABLED_UTILITY,
];

/** The surface/foreground token pair each variant adds on top of the base. */
const VARIANT_CLASSES = {
  primary: ["bg-primary", "text-primary-foreground"],
  secondary: ["bg-secondary", "text-secondary-foreground"],
  destructive: ["bg-destructive", "text-destructive-foreground"],
} as const;

/** The full expected class set for a variant, order-free. */
function expectedClasses(variant: keyof typeof VARIANT_CLASSES): string[] {
  return [...BASE_CLASSES, ...VARIANT_CLASSES[variant]].toSorted();
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlyButton(container: HTMLElement): HTMLButtonElement {
  const button = container.querySelector("button");
  expect(button).not.toBeNull();
  return button as HTMLButtonElement;
}

describe("Button", () => {
  it("renders the primary recipe by default", async () => {
    const { container } = await render(<Button>Sign in</Button>);

    const button = onlyButton(container);
    expect(classSet(button)).toEqual(expectedClasses("primary"));
    expect(button.textContent).toBe("Sign in");
  });

  it("swaps to the secondary colour pair", async () => {
    const { container } = await render(<Button variant="secondary">Cancel</Button>);

    expect(classSet(onlyButton(container))).toEqual(expectedClasses("secondary"));
  });

  it("swaps to the destructive colour pair", async () => {
    const { container } = await render(<Button variant="destructive">Delete</Button>);

    expect(classSet(onlyButton(container))).toEqual(expectedClasses("destructive"));
  });

  it("keeps every class of the pre-rebuild recipe except the disabled-state swap", async () => {
    // The compat guarantee, stated as its own test so a future change to the
    // recipe has to acknowledge which of the 11 measured call sites it moves.
    const { container } = await render(<Button>Sign in</Button>);

    const button = onlyButton(container);
    for (const legacy of LEGACY_PRIMARY_RECIPE.split(" ")) {
      if (legacy !== LEGACY_DISABLED_UTILITY) {
        expect(button.classList.contains(legacy), legacy).toBe(true);
      }
    }

    expect(button.classList.contains(LEGACY_DISABLED_UTILITY)).toBe(false);
    expect(button.classList.contains(STATE_DISABLED_UTILITY)).toBe(true);
  });

  it("forwards native button attributes (type, disabled)", async () => {
    const { container } = await render(
      <Button type="submit" disabled>
        Submit
      </Button>,
    );

    const button = onlyButton(container);
    expect(button.type).toBe("submit");
    expect(button.disabled).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Button data-testid="login-submit">Sign in</Button>);

    expect(container.querySelector('[data-testid="login-submit"]')).not.toBeNull();
  });

  it("exposes the disabled state as a data attribute", async () => {
    // Base UI's state contract, and what `data-[disabled]:opacity-50` hooks off.
    // A hand-rolled <button disabled> renders no such attribute.
    const { container } = await render(<Button disabled>Submit</Button>);

    expect(onlyButton(container).getAttribute("data-disabled")).toBe("");
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<Button>Submit</Button>);

    expect(onlyButton(container).hasAttribute("data-disabled")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both `bg-primary` and `bg-accent` on
    // the element and fails here.
    const { container } = await render(<Button className="bg-accent">Sign in</Button>);

    const button = onlyButton(container);
    expect(button.classList.contains("bg-accent")).toBe(true);
    expect(button.classList.contains("bg-primary")).toBe(false);
    expect(button.classList.contains("rounded-md")).toBe(true);
    expect(button.classList.contains("text-primary-foreground")).toBe(true);
  });

  it("composes onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes. `nativeButton={false}` tells Base UI the rendered element is
    // not a <button>, which is what keeps it from logging a dev-mode error.
    const { container } = await render(
      <Button render={<a href="/docs" />} nativeButton={false}>
        Read the docs
      </Button>,
    );

    const anchor = container.querySelector("a");
    expect(anchor).not.toBeNull();
    expect(anchor?.getAttribute("href")).toBe("/docs");
    expect(classSet(anchor as Element)).toEqual(expectedClasses("primary"));
    expect(container.querySelector("button")).toBeNull();
  });

  it("invokes the caller's onClick", async () => {
    // Base UI merges its own handlers over the caller's; this pins that the
    // caller's handler still runs.
    const onClick = vi.fn();
    const { container } = await render(<Button onClick={onClick}>Sign in</Button>);

    await userEvent.click(onlyButton(container));

    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
