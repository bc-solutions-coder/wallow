import { render } from "@bc-solutions-coder/testing/render";
import { useState } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";

import { Textarea } from "./textarea";

/*
 * The markup-level half of the Textarea spec (Wallow-ov6w.1.3). Stories are this
 * component's render/interaction coverage; what is left for a spec file is the
 * three edges a story cannot express — the exact class SET, the cn()/
 * tailwind-merge override, and the presence/absence of the `data-disabled`
 * attribute — plus the controlled value/onChange pairing its one measured call
 * site uses. Spec shape follows input.test.tsx: browser project, nothing mocked,
 * the recipe asserted THROUGH the component, classes compared as an order-free
 * set because cn()/tailwind-merge may reorder.
 *
 * SOURCE OF THE RECIPE: `Input`'s, verbatim. The measured call site is
 * CreateInquiryForm's `inquiry-message` textarea (apps/wallow-web), which today
 * hand-carries the pre-rebuild Input string on a bare `<textarea>` precisely
 * because no catalog Textarea existed. Sharing Input's recipe is therefore the
 * compat guarantee, not a coincidence — the two controls must not drift.
 *
 * WHY `data-disabled` IS ASSERTED HERE: the catalog styles state off Base UI's
 * `data-*` attributes rather than the `:disabled` pseudo-class
 * (packages/ui/CLAUDE.md), and the inherited recipe carries
 * `data-[disabled]:opacity-50`. Base UI ships no textarea part to stamp that
 * attribute, so this component must stamp it itself; without it the inherited
 * utility is dead and a disabled Textarea would not dim while a disabled Input
 * does. The story `PaintedDisabled` pins the other end of that contract (the
 * computed opacity) against the real stylesheet.
 */

/** Input's recipe, which Textarea shares so the two controls cannot drift. */
const SHARED_INPUT_RECIPE =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50";

/** The two utilities a multi-line control adds on top of the shared recipe. */
const MULTILINE_UTILITIES = ["min-h-20", "resize-y"];

/** The full expected class set, order-free. */
function expectedClasses(): string[] {
  return [...SHARED_INPUT_RECIPE.split(" "), ...MULTILINE_UTILITIES].toSorted();
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlyTextarea(container: HTMLElement): HTMLTextAreaElement {
  const textarea = container.querySelector("textarea");
  expect(textarea).not.toBeNull();
  return textarea as HTMLTextAreaElement;
}

/** The controlled value/onChange pairing the inquiry-message call site uses. */
function ControlledTextarea(): ReturnType<typeof Textarea> {
  const [value, setValue] = useState("");

  return (
    <Textarea
      value={value}
      onChange={(event) => {
        setValue(event.target.value);
      }}
      data-testid="controlled-textarea"
    />
  );
}

describe("Textarea", () => {
  it("renders a native textarea carrying the recipe", async () => {
    const { container } = await render(<Textarea />);

    expect(classSet(onlyTextarea(container))).toEqual(expectedClasses());
  });

  it("keeps every utility of the shared Input recipe, adding only the multi-line pair", async () => {
    // The compat guarantee, stated as its own test so a future change to either
    // recipe has to acknowledge that it moves the other control too.
    const { container } = await render(<Textarea />);

    const textarea = onlyTextarea(container);
    for (const utility of SHARED_INPUT_RECIPE.split(" ")) {
      expect(textarea.classList.contains(utility), utility).toBe(true);
    }

    for (const utility of MULTILINE_UTILITIES) {
      expect(textarea.classList.contains(utility), utility).toBe(true);
    }
  });

  it("forwards native textarea attributes (id, name, placeholder, rows, required)", async () => {
    const { container } = await render(
      <Textarea id="message" name="message" placeholder="Your message" rows={6} required />,
    );

    const textarea = onlyTextarea(container);
    expect(textarea.id).toBe("message");
    expect(textarea.name).toBe("message");
    expect(textarea.placeholder).toBe("Your message");
    expect(textarea.rows).toBe(6);
    expect(textarea.required).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Textarea data-testid="inquiry-message" />);

    expect(container.querySelector('[data-testid="inquiry-message"]')).not.toBeNull();
  });

  it("exposes the disabled state as a data attribute", async () => {
    // What `data-[disabled]:opacity-50` hooks off. Base UI stamps this on Input;
    // a native textarea does not, so the component has to.
    const { container } = await render(<Textarea disabled />);

    const textarea = onlyTextarea(container);
    expect(textarea.disabled).toBe(true);
    expect(textarea.getAttribute("data-disabled")).toBe("");
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<Textarea />);

    expect(onlyTextarea(container).hasAttribute("data-disabled")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both `bg-background` and `bg-accent`
    // on the element and fails here.
    const { container } = await render(<Textarea className="bg-accent" />);

    const textarea = onlyTextarea(container);
    expect(textarea.classList.contains("bg-accent")).toBe(true);
    expect(textarea.classList.contains("bg-background")).toBe(false);
    expect(textarea.classList.contains("rounded-md")).toBe(true);
    expect(textarea.classList.contains("min-h-20")).toBe(true);
  });

  it("lets a caller className override the multi-line utilities", async () => {
    // `min-h-20` and `resize-y` are recipe decisions, not laws: a call site that
    // wants a taller box or a locked one must be able to say so.
    const { container } = await render(<Textarea className="min-h-40 resize-none" />);

    const textarea = onlyTextarea(container);
    expect(textarea.classList.contains("min-h-40")).toBe(true);
    expect(textarea.classList.contains("min-h-20")).toBe(false);
    expect(textarea.classList.contains("resize-none")).toBe(true);
    expect(textarea.classList.contains("resize-y")).toBe(false);
  });

  it("drives a caller-controlled value through the caller's onChange", async () => {
    // The shape the inquiry-message call site uses: `value={state}` plus an
    // `onChange` that reads `event.target.value`.
    const { container } = await render(<ControlledTextarea />);

    const textarea = onlyTextarea(container);
    await userEvent.fill(textarea, "We need a BFF frontend.");

    expect(textarea.value).toBe("We need a BFF frontend.");
  });
});
