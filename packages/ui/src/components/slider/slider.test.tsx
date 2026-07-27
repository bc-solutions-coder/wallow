import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Slider, type SliderRootProps } from "./slider";

/*
 * Follows the exemplar spec shape from Wallow-m5aq.2.1 (button.test.tsx):
 * browser project, nothing mocked, recipes asserted THROUGH the component, and
 * class assertions as an order-free SET so tailwind-merge may reorder.
 *
 * ONE DEPARTURE, forced by how a slider is built. Every interaction here is a
 * KEYBOARD interaction on the thumb's hidden `<input type="range">`, never a
 * press or a drag on the control. Two reasons, both measured against the real
 * (unstyled) Base UI part before this spec was written:
 *
 * 1. Tailwind's stylesheet is NOT loaded in the vitest browser project
 *    (Wallow-m5aq.2.7 proved this wave-wide), so an unstyled control is a 0x0
 *    box and Playwright's actionability check refuses to press it —
 *    `userEvent.click` would hang for the full timeout no matter how correct
 *    the component is.
 * 2. A drag is worse than merely unclickable: Base UI computes the new value
 *    from the control's `getBoundingClientRect()`, so on a 0x0 control every
 *    pointer position maps to the same value and the assertion would be
 *    meaningless even if the events landed.
 *
 * The keyboard path needs no hit area and exercises the same value pipeline
 * (`onValueChange` -> `Slider.Value` -> the indicator's inline size), so it is
 * the honest place to pin behaviour. The POINTER half — that a press on the
 * track really moves the thumb, at a real size — belongs to
 * `slider.stories.tsx`, which renders under the real Tailwind pipeline.
 *
 * Keyboard steps verified against the unstyled part: ArrowRight/ArrowUp +step,
 * PageUp/PageDown +/-largeStep (default 10), Home -> min, End -> max.
 */

/** The root wrapper's utilities. Single source of truth for its assertions. */
const ROOT_CLASSES = ["flex", "w-full", "flex-col", "gap-2", "data-[disabled]:opacity-50"];

/** The label's utilities. */
const LABEL_CLASSES = ["text-sm", "font-medium", "text-foreground"];

/** The readout's utilities. `tabular-nums` stops the row twitching as digits change. */
const VALUE_CLASSES = ["text-sm", "tabular-nums", "text-muted-foreground"];

/**
 * The control's utilities. `touch-none` is not decoration: without it a drag on
 * a touch device scrolls the page instead of moving the thumb. The vertical
 * modifiers are how the component honours `orientation` — Base UI publishes it
 * as `data-orientation`, so it belongs here rather than in a cva variant.
 */
const CONTROL_CLASSES = [
  "flex",
  "w-full",
  "touch-none",
  "select-none",
  "items-center",
  "py-3",
  "data-[orientation=vertical]:h-48",
  "data-[orientation=vertical]:w-auto",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:px-3",
];

/** The rail's utilities: the full min..max range, thin on its cross axis. */
const TRACK_CLASSES = [
  "h-1.5",
  "w-full",
  "rounded-full",
  "bg-input",
  "data-[orientation=vertical]:h-full",
  "data-[orientation=vertical]:w-1.5",
];

/**
 * The filled portion's utilities — colour and shape ONLY. Base UI writes the
 * indicator's position and size as inline styles from the current value, so any
 * sizing utility here would either be overridden or fight it.
 */
const INDICATOR_CLASSES = ["rounded-full", "bg-primary"];

/** The handle's utilities. Its own size, since the track it sits on is thinner. */
const THUMB_CLASSES = [
  "size-4",
  "rounded-full",
  "border",
  "border-border",
  "bg-background",
  "shadow-sm",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** A `className` for each part, so one render can style exactly one of them. */
interface PartClassNames {
  readonly root?: string;
  readonly label?: string;
  readonly value?: string;
  readonly control?: string;
  readonly track?: string;
  readonly indicator?: string;
  readonly thumb?: string;
}

interface RenderedSlider {
  readonly container: HTMLElement;
  readonly root: HTMLElement;
  readonly label: HTMLElement;
  readonly value: HTMLElement;
  readonly control: HTMLElement;
  readonly track: HTMLElement;
  readonly indicator: HTMLElement;
  readonly thumb: HTMLElement;
  /** The hidden `<input type="range">` inside the thumb: value, form wiring, keyboard. */
  readonly input: HTMLInputElement;
  /** Every part, for the state-attribute sweeps. */
  readonly parts: readonly HTMLElement[];
}

/** The full anatomy, in the nesting Base UI documents. */
async function renderSlider(
  props: SliderRootProps = {},
  classNames: PartClassNames = {},
): Promise<RenderedSlider> {
  const { container } = await render(
    <Slider.Root data-testid="slider-root" className={classNames.root} {...props}>
      <Slider.Label data-testid="slider-label" className={classNames.label}>
        Volume
      </Slider.Label>
      <Slider.Value data-testid="slider-value" className={classNames.value} />
      <Slider.Control data-testid="slider-control" className={classNames.control}>
        <Slider.Track data-testid="slider-track" className={classNames.track}>
          <Slider.Indicator data-testid="slider-indicator" className={classNames.indicator} />
          <Slider.Thumb data-testid="slider-thumb" className={classNames.thumb} />
        </Slider.Track>
      </Slider.Control>
    </Slider.Root>,
  );

  function part(testId: string): HTMLElement {
    const element = container.querySelector(`[data-testid="${testId}"]`);
    expect(element, testId).not.toBeNull();
    return element as HTMLElement;
  }

  const root = part("slider-root");
  const label = part("slider-label");
  const value = part("slider-value");
  const control = part("slider-control");
  const track = part("slider-track");
  const indicator = part("slider-indicator");
  const thumb = part("slider-thumb");
  const input = thumb.querySelector("input");
  expect(input, "hidden range input").not.toBeNull();

  return {
    container,
    root,
    label,
    value,
    control,
    track,
    indicator,
    thumb,
    input: input as HTMLInputElement,
    parts: [root, label, value, control, track, indicator, thumb],
  };
}

describe("Slider", () => {
  it("renders every part's recipe on its own element", async () => {
    const { root, label, value, control, track, indicator, thumb } = await renderSlider();

    expect(classSet(root), "root").toEqual(ROOT_CLASSES.toSorted());
    expect(classSet(label), "label").toEqual(LABEL_CLASSES.toSorted());
    expect(classSet(value), "value").toEqual(VALUE_CLASSES.toSorted());
    expect(classSet(control), "control").toEqual(CONTROL_CLASSES.toSorted());
    expect(classSet(track), "track").toEqual(TRACK_CLASSES.toSorted());
    expect(classSet(indicator), "indicator").toEqual(INDICATOR_CLASSES.toSorted());
    expect(classSet(thumb), "thumb").toEqual(THUMB_CLASSES.toSorted());
  });

  it("renders the anatomy Base UI documents", async () => {
    // The elements are not interchangeable: the readout must be an <output> for
    // a screen reader to announce it as one, and the value has to reach a real
    // <input type="range"> for the form and for the keyboard.
    const { root, value, thumb, input } = await renderSlider({ defaultValue: 30 });

    expect(root.getAttribute("role")).toBe("group");
    expect(value.tagName).toBe("OUTPUT");
    expect(input.type).toBe("range");
    expect(input.value).toBe("30");
    expect(thumb.contains(input)).toBe(true);
  });

  it("associates the label with the thumb's input", async () => {
    // A plain <label for> cannot reach the input, which Base UI generates the
    // id for. Slider.Label is the only labelling path, so it is worth pinning.
    const { root, label, input } = await renderSlider();

    expect(label.id).not.toBe("");
    expect(root.getAttribute("aria-labelledby")).toBe(label.id);
    expect(input.getAttribute("aria-labelledby")).toBe(label.id);
  });

  it("shows the current value in the readout", async () => {
    const { value } = await renderSlider({ defaultValue: 30 });

    expect(value.textContent).toBe("30");
  });

  it("formats the readout through Intl", async () => {
    // `format` is the reason the readout is its own part rather than a caller's
    // {value}: the same options drive the thumb's aria-valuetext.
    const { value } = await renderSlider({
      defaultValue: 0.25,
      max: 1,
      step: 0.01,
      format: {
        style: "percent",
      },
    });

    expect(value.textContent).toBe("25%");
  });

  it("steps by one and updates the readout from the keyboard", async () => {
    const { input, value } = await renderSlider({ defaultValue: 50 });

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(input.value).toBe("51");
    expect(value.textContent).toBe("51");
  });

  it("steps by the caller's step", async () => {
    const { input } = await renderSlider({ defaultValue: 50, step: 10 });

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(input.value).toBe("60");
  });

  it("steps by largeStep on Page Up and Page Down", async () => {
    const { input } = await renderSlider({ defaultValue: 50 });

    input.focus();
    await userEvent.keyboard("{PageUp}");
    expect(input.value).toBe("60");

    await userEvent.keyboard("{PageDown}");
    expect(input.value).toBe("50");
  });

  it("jumps to min and max on Home and End", async () => {
    const { input } = await renderSlider({ defaultValue: 50, min: 10, max: 90 });

    input.focus();
    await userEvent.keyboard("{Home}");
    expect(input.value).toBe("10");

    await userEvent.keyboard("{End}");
    expect(input.value).toBe("90");
  });

  it("refuses to step past max", async () => {
    const { input } = await renderSlider({ defaultValue: 100 });

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(input.value).toBe("100");
  });

  it("reports the new value and the reason to onValueChange", async () => {
    const onValueChange = vi.fn();
    const { input } = await renderSlider({ defaultValue: 50, onValueChange });

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(onValueChange).toHaveBeenCalledTimes(1);
    const [value, details] = onValueChange.mock.calls[0] as [number, { reason: string }];
    expect(value).toBe(51);
    // The reason is how a caller tells a keyboard nudge from a drag, so it is
    // part of the contract rather than an implementation detail of Base UI's.
    expect(details.reason).toBe("keyboard");
  });

  it("leaves a controlled slider for its owner to update", async () => {
    // `value` without a state update must NOT move: the component may not keep
    // private state behind the caller's back.
    const onValueChange = vi.fn();
    const { input, value } = await renderSlider({ value: 50, onValueChange });

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(onValueChange.mock.calls[0]?.[0]).toBe(51);
    expect(input.value).toBe("50");
    expect(value.textContent).toBe("50");
  });

  it("exposes the disabled state on every part and refuses to move", async () => {
    const onValueChange = vi.fn();
    const { parts, input } = await renderSlider({
      defaultValue: 40,
      disabled: true,
      onValueChange,
    });

    expect(input.disabled).toBe(true);
    for (const element of parts) {
      expect(element.hasAttribute("data-disabled"), element.dataset["testid"]).toBe(true);
    }

    input.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(onValueChange).not.toHaveBeenCalled();
    expect(input.value).toBe("40");
  });

  it("publishes the orientation on every part", async () => {
    // The vertical utilities in the control and track recipes key off exactly
    // this attribute, so a part that failed to publish it would silently render
    // a vertical slider with horizontal geometry.
    const { parts } = await renderSlider({ orientation: "vertical" });

    for (const element of parts) {
      expect(element.getAttribute("data-orientation"), element.dataset["testid"]).toBe("vertical");
    }
  });

  it("carries name and value into a form submission", async () => {
    const { input } = await renderSlider({ defaultValue: 30, name: "volume" });

    expect(input.name).toBe("volume");
    expect(input.value).toBe("30");
    expect(input.min).toBe("0");
    expect(input.max).toBe("100");
  });

  it("renders one indexed thumb per value for a range slider", async () => {
    const { container } = await render(
      <Slider.Root defaultValue={[20, 60]}>
        <Slider.Control>
          <Slider.Track>
            <Slider.Indicator />
            <Slider.Thumb index={0} data-testid="slider-thumb-0" />
            <Slider.Thumb index={1} data-testid="slider-thumb-1" />
          </Slider.Track>
        </Slider.Control>
      </Slider.Root>,
    );

    const thumbs = [...container.querySelectorAll('[data-testid^="slider-thumb-"]')];
    expect(thumbs.map((thumb) => thumb.getAttribute("data-index"))).toEqual(["0", "1"]);
    expect(thumbs.map((thumb) => (thumb.querySelector("input") as HTMLInputElement).value)).toEqual(
      ["20", "60"],
    );
    for (const thumb of thumbs) {
      expect(classSet(thumb)).toEqual(THUMB_CLASSES.toSorted());
    }
  });

  /*
   * The cn()/tailwind-merge proof, one case per part: the conflicting utility is
   * REMOVED rather than appended after, and a utility the caller never mentioned
   * survives. A string-append implementation leaves BOTH classes on the element;
   * a part that forgot its recipe entirely fails on the survivor.
   */
  const overrides = [
    { part: "root", className: "gap-6", overridden: "gap-2", survivor: "flex" },
    { part: "label", className: "text-lg", overridden: "text-sm", survivor: "font-medium" },
    {
      part: "value",
      className: "text-foreground",
      overridden: "text-muted-foreground",
      survivor: "tabular-nums",
    },
    { part: "control", className: "py-0", overridden: "py-3", survivor: "touch-none" },
    { part: "track", className: "bg-accent", overridden: "bg-input", survivor: "rounded-full" },
    {
      part: "indicator",
      className: "bg-accent",
      overridden: "bg-primary",
      survivor: "rounded-full",
    },
    { part: "thumb", className: "size-8", overridden: "size-4", survivor: "border-border" },
  ] as const;

  for (const { part, className, overridden, survivor } of overrides) {
    it(`lets a caller className override the ${part} recipe's ${overridden}`, async () => {
      const rendered = await renderSlider({}, { [part]: className });
      const element = rendered[part];

      expect(element.classList.contains(className), className).toBe(true);
      expect(element.classList.contains(overridden), `${overridden} removed`).toBe(false);
      expect(element.classList.contains(survivor), `${survivor} survives`).toBe(true);
    });
  }

  it("composes parts onto other elements through the render prop", async () => {
    const { container } = await render(
      <Slider.Root render={<section />} defaultValue={30}>
        <Slider.Control>
          <Slider.Track render={<span />} data-testid="slider-track" />
        </Slider.Control>
      </Slider.Root>,
    );

    const section = container.querySelector("section");
    const track = container.querySelector('[data-testid="slider-track"]');
    expect(section?.getAttribute("role")).toBe("group");
    expect(classSet(section as Element), "root recipe").toEqual(ROOT_CLASSES.toSorted());
    expect(track?.tagName, "track element").toBe("SPAN");
    expect(classSet(track as Element), "track recipe").toEqual(TRACK_CLASSES.toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <Slider.Root data-testid="settings-volume">
        <Slider.Control>
          <Slider.Track>
            <Slider.Thumb />
          </Slider.Track>
        </Slider.Control>
      </Slider.Root>,
    );

    expect(container.querySelector('[data-testid="settings-volume"]')).not.toBeNull();
  });
});
