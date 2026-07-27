import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { RadioGroup } from "../radio-group";
import { Radio } from "./radio";

/*
 * Radio behavioural spec (Wallow-m5aq.2.6), shaped after the Wallow-m5aq.2.1
 * Button exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. The recipes are asserted THROUGH the component, never by importing
 *      `radioRootRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder.
 *   4. Stories carry the visual coverage (see radio.stories.tsx); this file is
 *      only for the edges a screenshot cannot make: state data-attributes, the
 *      caller-className override, indicator mounting, and the `render` prop.
 *
 * ANATOMY, verified against @base-ui/react 1.6.0 in this browser (not guessed):
 *   <div role="radiogroup">                        <- RadioGroup
 *     <span role="radio" data-unchecked|data-checked>  <- Radio.Root
 *       <span data-checked>                            <- Radio.Indicator
 *     <input type="radio" name value aria-hidden>   <- SIBLING of the root, not a child
 * The indicator is unmounted entirely while unselected unless `keepMounted` is
 * passed, and a selected root also picks up `data-composite-item-active`.
 */

/** Utilities `Radio.Root` must render. */
const ROOT_CLASSES = [
  "inline-flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "rounded-full",
  "border",
  "border-input",
  "bg-background",
  "data-[checked]:border-primary",
  "data-[checked]:bg-primary",
  "data-[disabled]:opacity-50",
];

/** Utilities `Radio.Indicator` must render. */
const INDICATOR_CLASSES = [
  "size-2",
  "rounded-full",
  "bg-primary-foreground",
  "data-[unchecked]:hidden",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The radio carrying `data-testid`, failing loudly rather than returning null. */
function radioByTestId(container: HTMLElement, testId: string): HTMLElement {
  const radio = container.querySelector(`[data-testid="${testId}"]`);
  expect(radio, testId).not.toBeNull();
  return radio as HTMLElement;
}

/** A two-radio group, the smallest arrangement in which a radio is meaningful. */
function FruitGroup({
  defaultValue,
  radioProps,
}: {
  readonly defaultValue?: string;
  readonly radioProps?: Partial<Parameters<typeof Radio.Root>[0]>;
}) {
  return (
    <RadioGroup name="fruit" defaultValue={defaultValue} aria-label="Fruit">
      <Radio.Root value="apple" data-testid="apple" {...radioProps}>
        <Radio.Indicator data-testid="apple-indicator" />
      </Radio.Root>
      <Radio.Root value="pear" data-testid="pear">
        <Radio.Indicator data-testid="pear-indicator" />
      </Radio.Root>
    </RadioGroup>
  );
}

describe("Radio", () => {
  it("renders the root recipe on Base UI's radio element", async () => {
    const { container } = await render(<FruitGroup />);

    const apple = radioByTestId(container, "apple");
    expect(apple.getAttribute("role")).toBe("radio");
    expect(classSet(apple)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("marks the selected radio checked and the rest unchecked", async () => {
    // Base UI's state contract, and what `data-[checked]:bg-primary` hooks off.
    const { container } = await render(<FruitGroup defaultValue="pear" />);

    const apple = radioByTestId(container, "apple");
    const pear = radioByTestId(container, "pear");

    expect(pear.getAttribute("data-checked")).toBe("");
    expect(pear.hasAttribute("data-unchecked")).toBe(false);
    expect(pear.getAttribute("aria-checked")).toBe("true");

    expect(apple.getAttribute("data-unchecked")).toBe("");
    expect(apple.hasAttribute("data-checked")).toBe(false);
    expect(apple.getAttribute("aria-checked")).toBe("false");
  });

  it("mounts the indicator only while the radio is selected", async () => {
    const { container } = await render(<FruitGroup defaultValue="pear" />);

    expect(container.querySelector('[data-testid="pear-indicator"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="apple-indicator"]')).toBeNull();
  });

  it("renders the indicator recipe on the selected radio's dot", async () => {
    const { container } = await render(<FruitGroup defaultValue="pear" />);

    const indicator = radioByTestId(container, "pear-indicator");
    expect(classSet(indicator)).toEqual(INDICATOR_CLASSES.toSorted());
    expect(indicator.getAttribute("data-checked")).toBe("");
  });

  it("keeps a keepMounted indicator in the DOM, hidden by its unchecked rule", async () => {
    // `data-[unchecked]:hidden` only bites for callers that keep the dot
    // mounted — which is the only way to animate it out.
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit">
        <Radio.Root value="apple" data-testid="apple">
          <Radio.Indicator keepMounted data-testid="apple-indicator" />
        </Radio.Root>
      </RadioGroup>,
    );

    const indicator = radioByTestId(container, "apple-indicator");
    expect(indicator.getAttribute("data-unchecked")).toBe("");
    expect(indicator.classList.contains("data-[unchecked]:hidden")).toBe(true);
  });

  it("exposes the disabled state as a data attribute and on the hidden input", async () => {
    const { container } = await render(<FruitGroup radioProps={{ disabled: true }} />);

    expect(radioByTestId(container, "apple").getAttribute("data-disabled")).toBe("");

    const input = container.querySelector("input");
    expect(input).not.toBeNull();
    expect((input as HTMLInputElement).disabled).toBe(true);
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<FruitGroup />);

    expect(radioByTestId(container, "apple").hasAttribute("data-disabled")).toBe(false);
  });

  it("exposes the readOnly and required states as data attributes", async () => {
    const { container: readOnly } = await render(<FruitGroup radioProps={{ readOnly: true }} />);
    expect(radioByTestId(readOnly, "apple").getAttribute("data-readonly")).toBe("");

    const { container: required } = await render(<FruitGroup radioProps={{ required: true }} />);
    expect(radioByTestId(required, "apple").getAttribute("data-required")).toBe("");
  });

  it("publishes the radio's value on the hidden input under the group name", async () => {
    // The reason a radio is form-usable at all: the visible control is a span,
    // so Base UI renders a real <input type="radio"> beside it.
    const { container } = await render(<FruitGroup defaultValue="pear" />);

    const input = container.querySelector("input");
    expect(input).not.toBeNull();
    expect((input as HTMLInputElement).type).toBe("radio");
    expect((input as HTMLInputElement).name).toBe("fruit");
    expect((input as HTMLInputElement).value).toBe("apple");
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, untouched utilities survive, and the
    // modifier-scoped `data-[checked]:` rules are left alone because they are a
    // different tailwind-merge group. A string-append implementation fails here.
    const { container } = await render(<FruitGroup radioProps={{ className: "bg-accent" }} />);

    const apple = radioByTestId(container, "apple");
    expect(apple.classList.contains("bg-accent")).toBe(true);
    expect(apple.classList.contains("bg-background")).toBe(false);
    expect(apple.classList.contains("rounded-full")).toBe(true);
    expect(apple.classList.contains("border-input")).toBe(true);
    expect(apple.classList.contains("data-[checked]:bg-primary")).toBe(true);
  });

  it("composes onto another element through the render prop", async () => {
    // The recipe has to travel to whatever element the caller substitutes, and
    // the `radio` role has to come with it.
    const { container } = await render(<FruitGroup radioProps={{ render: <div /> }} />);

    const apple = radioByTestId(container, "apple");
    expect(apple.tagName).toBe("DIV");
    expect(apple.getAttribute("role")).toBe("radio");
    expect(classSet(apple)).toEqual(ROOT_CLASSES.toSorted());
  });
});
