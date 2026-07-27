import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Meter } from "./meter";

/*
 * Wallow-m5aq.4.4 — Meter, the paired half of the Progress task. Same spec shape
 * as the Wave-1 exemplar (Wallow-m5aq.2.1) and the Wave-2 exemplar
 * (Wallow-m5aq.3.1): browser vitest project, nothing mocked, the recipes
 * asserted THROUGH the component, class assertions as an order-free set.
 *
 * Meter is a VALUE-DISPLAY component, not an overlay and not interactive:
 * nothing is portalled, nothing opens or closes, and there is no keyboard
 * contract, so every query goes through render()'s `container` and none of the
 * Wave-2 popup gotchas apply. Every assertion below is a synchronous read.
 *
 * ANATOMY, measured against the installed Base UI 1.6.0 rather than read off the
 * docs (a throwaway probe spec, since deleted):
 *
 *   <div role="meter" aria-valuemin="0" aria-valuemax="100" aria-valuenow="30"
 *        aria-valuetext="30%" aria-labelledby="…">                        <- Root
 *     <span role="presentation" id="…">Storage</span>                     <- Label
 *     <span aria-hidden="true">30%</span>                                 <- Value
 *     <div>                                                               <- Track
 *       <div style="…; width: 30%">                                       <- Indicator
 *     <span role="presentation" style="clip-path: inset(50%); …">x</span> <- Base UI's own
 *
 * Four measurements are worth stating, because each is easy to assume wrong:
 *   - METER PUBLISHES NO `data-*` STATE ON ANY PART. `MeterRootState` is the
 *     empty interface and `MeterRoot` passes no `stateAttributesMapping` at all,
 *     so unlike Progress (`data-progressing` / `data-complete` /
 *     `data-indeterminate` on all five parts) there is nothing here for a
 *     `data-[…]:` modifier to key off. Any such modifier in a meter recipe would
 *     be dead code, and the class-set assertions below are what keep one out.
 *   - THE VALUE IS CLAMPED, NOT REJECTED: `value={150}` with `max={50}` renders
 *     `aria-valuenow="50"`, `aria-valuetext="100%"` and a full-width indicator.
 *   - `Meter.Value`'s default text is the POSITION IN THE RANGE as a percent
 *     (value 30 of 0..100 → "30%", value 150 of 0..50 → "100%"), so it always
 *     agrees with the indicator's width. Progress does the OPPOSITE — it formats
 *     the raw number. The two components genuinely disagree; do not "fix" one to
 *     match the other. Passing `format` switches Meter to formatting the raw
 *     value instead, and the readout then stops tracking the fill.
 *   - `Meter.Root` ALWAYS appends a visually-hidden `<span>x</span>` of its own
 *     after the caller's children, so `root.textContent` has a trailing "x".
 *     Assert on parts, not on the root's text.
 *
 * Base UI stamps NO class of its own on any of the five parts (probed), so every
 * class set below is pure recipe and is asserted with no spread-in extras.
 */

/** Every utility `Meter.Root` must render. Single source of truth. */
const ROOT_CLASSES = ["flex", "w-full", "flex-col", "gap-2"];

/** Every utility `Meter.Label` must render. */
const LABEL_CLASSES = ["text-sm", "font-medium", "text-foreground"];

/** Every utility `Meter.Value` must render. `tabular-nums` keeps digits from twitching. */
const VALUE_CLASSES = ["text-sm", "tabular-nums", "text-muted-foreground"];

/**
 * Every utility `Meter.Track` must render. `overflow-hidden` clips the
 * indicator's rounded fill; the explicit `h-2` is what the indicator's inline
 * `height: inherit` resolves against.
 */
const TRACK_CLASSES = ["h-2", "w-full", "overflow-hidden", "rounded-full", "bg-input"];

/**
 * Every utility `Meter.Indicator` must render — colour, shape and the width
 * TRANSITION only, since the width itself is Base UI's inline style. Note the
 * absence of any `data-[…]:` modifier: see the header, a meter has no states.
 */
const INDICATOR_CLASSES = [
  "rounded-full",
  "bg-primary",
  "transition-[width]",
  "duration-200",
  "ease-out",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function part(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

interface FixtureProps {
  readonly value?: number;
  readonly min?: number;
  readonly max?: number;
  readonly format?: Intl.NumberFormatOptions;
  readonly locale?: Intl.LocalesArgument;
  readonly getAriaValueText?: (formattedValue: string, value: number) => string;
  readonly trackClassName?: string;
}

/** The complete five-part fixture every case below starts from. */
function renderMeter({ trackClassName, ...rootProps }: FixtureProps = {}) {
  return render(
    <Meter.Root value={30} data-testid="root" {...rootProps}>
      <Meter.Label data-testid="label">Storage used</Meter.Label>
      <Meter.Value data-testid="value" />
      <Meter.Track data-testid="track" className={trackClassName}>
        <Meter.Indicator data-testid="indicator" />
      </Meter.Track>
    </Meter.Root>,
  );
}

/** The indicator's inline `width`, e.g. "30%". */
function indicatorWidth(container: HTMLElement): string {
  return part(container, "indicator").style.width;
}

describe("Meter", () => {
  describe("anatomy", () => {
    it("mirrors Base UI's five namespace members exactly", () => {
      expect(Object.keys(Meter).toSorted()).toEqual([
        "Indicator",
        "Label",
        "Root",
        "Track",
        "Value",
      ]);
    });

    it("renders role=meter with the full ARIA value contract", async () => {
      // role="meter", NOT role="progressbar" — the whole reason this is a second
      // component rather than a variant of Progress.
      const { container } = await renderMeter();

      const root = part(container, "root");
      expect(root.tagName).toBe("DIV");
      expect(root.getAttribute("role")).toBe("meter");
      expect(root.getAttribute("aria-valuemin")).toBe("0");
      expect(root.getAttribute("aria-valuemax")).toBe("100");
      expect(root.getAttribute("aria-valuenow")).toBe("30");
      expect(root.getAttribute("aria-valuetext")).toBe("30%");
    });

    it("names the meter through the label rather than a caller-owned id pair", async () => {
      const { container } = await renderMeter();

      const label = part(container, "label");
      expect(label.tagName).toBe("SPAN");
      expect(label.getAttribute("role")).toBe("presentation");
      expect(label.getAttribute("id")).not.toBeNull();
      expect(part(container, "root").getAttribute("aria-labelledby")).toBe(
        label.getAttribute("id"),
      );
    });

    it("hides the visible readout from screen readers", async () => {
      const { container } = await renderMeter();

      const value = part(container, "value");
      expect(value.tagName).toBe("SPAN");
      expect(value.getAttribute("aria-hidden")).toBe("true");
      expect(value.textContent).toBe("30%");
    });

    it("nests the indicator inside the track", async () => {
      const { container } = await renderMeter();

      expect(part(container, "indicator").parentElement).toBe(part(container, "track"));
    });

    it("publishes NO state attribute on any part", async () => {
      // The asymmetry with Progress, pinned: a `data-[…]:` modifier in a meter
      // recipe would never match, so none of them may appear in one.
      const { container } = await renderMeter();

      for (const testId of ["root", "label", "value", "track", "indicator"]) {
        const names = [...part(container, testId).attributes].map((attribute) => attribute.name);
        expect(
          names.filter((name) => name.startsWith("data-") && name !== "data-testid"),
          testId,
        ).toEqual([]);
      }
    });
  });

  describe("range and clamping", () => {
    it("measures the indicator against min/max", async () => {
      const { container } = await renderMeter({ value: 5, min: 0, max: 20 });

      expect(indicatorWidth(container)).toBe("25%");
      expect(part(container, "root").getAttribute("aria-valuenow")).toBe("5");
    });

    it("clamps a value above max instead of overflowing the track", async () => {
      const { container } = await renderMeter({ value: 150, min: 0, max: 50 });

      const root = part(container, "root");
      expect(root.getAttribute("aria-valuenow")).toBe("50");
      expect(root.getAttribute("aria-valuetext")).toBe("100%");
      expect(indicatorWidth(container)).toBe("100%");
    });

    it("clamps a value below min to an empty indicator", async () => {
      const { container } = await renderMeter({ value: -10, min: 0, max: 50 });

      expect(part(container, "root").getAttribute("aria-valuenow")).toBe("0");
      expect(indicatorWidth(container)).toBe("0%");
    });

    it("offsets the fill by a non-zero min", async () => {
      // 60 of 50..100 is a fifth of the way along, not 60% of it.
      const { container } = await renderMeter({ value: 60, min: 50, max: 100 });

      expect(indicatorWidth(container)).toBe("20%");
      expect(part(container, "value").textContent).toBe("20%");
    });

    it("is sized by Base UI's inline style, never by a class", async () => {
      const { container } = await renderMeter();

      const indicator = part(container, "indicator");
      expect(indicator.style.width).toBe("30%");
      expect(indicator.style.height).toBe("inherit");
      expect(indicator.style.insetInlineStart).toBe("0px");
    });
  });

  describe("value formatting", () => {
    it("reads the position in the range by default, so it agrees with the fill", async () => {
      const { container } = await renderMeter({ value: 150, min: 0, max: 50 });

      expect(part(container, "value").textContent).toBe("100%");
      expect(indicatorWidth(container)).toBe("100%");
    });

    it("formats the RAW value once a caller passes format options", async () => {
      // The trade-off worth knowing: with `format`, the readout stops tracking
      // the fill and starts reporting the underlying quantity.
      const { container } = await render(
        <Meter.Root
          value={30}
          max={50}
          format={{ style: "unit", unit: "gigabyte" }}
          locale="en-US"
          data-testid="root"
        >
          <Meter.Value data-testid="value" />
          <Meter.Track data-testid="track">
            <Meter.Indicator data-testid="indicator" />
          </Meter.Track>
        </Meter.Root>,
      );

      expect(part(container, "value").textContent).toBe("30 GB");
      expect(indicatorWidth(container)).toBe("60%");
    });

    it("passes the formatted value and the raw value to a children function", async () => {
      const { container } = await render(
        <Meter.Root value={30} data-testid="root">
          <Meter.Value data-testid="value">
            {(formattedValue, value) => `${formattedValue} full (raw ${value})`}
          </Meter.Value>
        </Meter.Root>,
      );

      expect(part(container, "value").textContent).toBe("30% full (raw 30)");
    });

    it("lets a caller replace the announced text through getAriaValueText", async () => {
      const { container } = await renderMeter({
        value: 30,
        getAriaValueText: (formattedValue) => `${formattedValue} of your quota`,
      });

      expect(part(container, "root").getAttribute("aria-valuetext")).toBe("30% of your quota");
    });
  });

  describe("recipes", () => {
    it("paints all five parts", async () => {
      const { container } = await renderMeter();

      expect(classSet(part(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(part(container, "label"))).toEqual(LABEL_CLASSES.toSorted());
      expect(classSet(part(container, "value"))).toEqual(VALUE_CLASSES.toSorted());
      expect(classSet(part(container, "track"))).toEqual(TRACK_CLASSES.toSorted());
      expect(classSet(part(container, "indicator"))).toEqual(INDICATOR_CLASSES.toSorted());
    });

    it("paints every part the same at every value", async () => {
      for (const value of [0, 30, 100]) {
        const { container } = await renderMeter({ value });

        expect(classSet(part(container, "indicator")), `${value}`).toEqual(
          INDICATOR_CLASSES.toSorted(),
        );
        expect(classSet(part(container, "track")), `${value}`).toEqual(TRACK_CLASSES.toSorted());
      }
    });
  });

  describe("composition", () => {
    it("lets a caller className override a recipe utility on the track", async () => {
      // The cn()/tailwind-merge proof: the conflicting height utility is REMOVED
      // rather than appended after, while utilities the caller never mentioned survive.
      const { container } = await renderMeter({ trackClassName: "h-3" });

      const track = part(container, "track");
      expect(track.classList.contains("h-3")).toBe(true);
      expect(track.classList.contains("h-2")).toBe(false);
      expect(track.classList.contains("rounded-full")).toBe(true);
      expect(track.classList.contains("bg-input")).toBe(true);
    });

    it("composes the root recipe onto another element through the render prop", async () => {
      const { container } = await render(
        <Meter.Root value={30} render={<section data-testid="root" />}>
          <Meter.Track data-testid="track" />
        </Meter.Root>,
      );

      const section = container.querySelector("section");
      expect(section).not.toBeNull();
      expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    });

    it("passes an app-owned aria-label and data-testid through to the root", async () => {
      const { container } = await render(
        <Meter.Root value={30} data-testid="storage-meter" aria-label="Storage used">
          <Meter.Track />
        </Meter.Root>,
      );

      expect(part(container, "storage-meter").getAttribute("aria-label")).toBe("Storage used");
    });
  });
});
