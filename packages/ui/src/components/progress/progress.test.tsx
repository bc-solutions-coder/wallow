import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Progress } from "./progress";

/*
 * Wallow-m5aq.4.4 — Progress. Same spec shape as the Wave-1 exemplar
 * (Wallow-m5aq.2.1) and the Wave-2 exemplar (Wallow-m5aq.3.1): browser vitest
 * project, nothing mocked, the recipes asserted THROUGH the component, class
 * assertions as an order-free set.
 *
 * Progress is a VALUE-DISPLAY component, not an overlay and not interactive:
 * nothing is portalled, nothing opens or closes, and there is no keyboard
 * contract, so every query goes through render()'s `container` and none of the
 * Wave-2 popup gotchas apply. There is correspondingly nothing to poll for —
 * every assertion below is a synchronous read of a single render.
 *
 * ANATOMY, measured against the installed Base UI 1.6.0 rather than read off the
 * docs (a throwaway probe spec, since deleted):
 *
 *   <div role="progressbar" data-progressing aria-valuemin="0" aria-valuemax="100"
 *        aria-valuenow="40" aria-valuetext="40%" aria-labelledby="…">     <- Root
 *     <span role="presentation" id="…" data-progressing>                  <- Label
 *     <span aria-hidden="true" data-progressing>40%</span>                <- Value
 *     <div data-progressing>                                              <- Track
 *       <div data-progressing style="…; width: 40%">                      <- Indicator
 *     <span role="presentation" style="clip-path: inset(50%); …">x</span> <- Base UI's own
 *
 * Five measurements are worth stating, because each is easy to assume wrong:
 *   - the STATUS is three MUTUALLY EXCLUSIVE BARE attributes — `data-indeterminate`
 *     (value === null), `data-progressing`, `data-complete` (value === max) — and
 *     NOT a `data-status="…"` value attribute. They land on ALL FIVE parts, not
 *     just the root, so a recipe on any part may key off them.
 *   - the INDICATOR IS SIZED BY AN INLINE STYLE, not by a class: Base UI writes
 *     `insetInlineStart: 0; height: inherit; width: <percent>%` computed from
 *     value/min/max. The recipe therefore paints only — any width utility in it
 *     is dead weight in the determinate case. While INDETERMINATE the inline
 *     style is empty `{}`, which is the one case a `data-[indeterminate]:` width
 *     utility can win, and why the indicator recipe carries one.
 *   - `Progress.Value`'s text is the RAW VALUE formatted as a percent, NOT its
 *     position in the range: at value=5 min=0 max=20 the Value reads "5%" while
 *     the Indicator is 25% wide. (Meter does the opposite — it formats the
 *     position. The two components genuinely disagree; do not "fix" one to match.)
 *   - `Progress.Root` ALWAYS appends a visually-hidden `<span>x</span>` of its own
 *     after the caller's children, so `root.textContent` has a trailing "x" and
 *     `root.children` has one more entry than the JSX shows. Assert on parts, not
 *     on the root's text or child count.
 *   - `Progress.Value` is `aria-hidden="true"`; the announced readout is the
 *     root's `aria-valuetext`. A spec that reads the Value for accessibility
 *     would be testing the wrong element.
 *
 * Base UI stamps NO class of its own on any of the five parts (probed), so every
 * class set below is pure recipe and is asserted with no spread-in extras.
 */

/** Every utility `Progress.Root` must render. Single source of truth. */
const ROOT_CLASSES = ["flex", "w-full", "flex-col", "gap-2"];

/** Every utility `Progress.Label` must render. */
const LABEL_CLASSES = ["text-sm", "font-medium", "text-foreground"];

/** Every utility `Progress.Value` must render. `tabular-nums` keeps digits from twitching. */
const VALUE_CLASSES = ["text-sm", "tabular-nums", "text-muted-foreground"];

/**
 * Every utility `Progress.Track` must render. `overflow-hidden` clips the
 * indicator's rounded fill; the explicit `h-2` is what the indicator's inline
 * `height: inherit` resolves against.
 */
const TRACK_CLASSES = ["h-2", "w-full", "overflow-hidden", "rounded-full", "bg-input"];

/**
 * Every utility `Progress.Indicator` must render — colour, shape and the width
 * TRANSITION only, since the width itself is Base UI's inline style. The two
 * `data-[indeterminate]:` utilities are the exception that proves the rule: they
 * apply exactly when Base UI writes no inline style at all.
 */
const INDICATOR_CLASSES = [
  "rounded-full",
  "bg-primary",
  "transition-[width]",
  "duration-200",
  "ease-out",
  "data-[indeterminate]:w-full",
  "data-[indeterminate]:animate-pulse",
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
  readonly value?: number | null;
  readonly min?: number;
  readonly max?: number;
  readonly format?: Intl.NumberFormatOptions;
  readonly locale?: Intl.LocalesArgument;
  readonly getAriaValueText?: (formattedValue: string | null, value: number | null) => string;
  readonly trackClassName?: string;
}

/** The complete five-part fixture every case below starts from. */
function renderProgress({ trackClassName, ...rootProps }: FixtureProps = {}) {
  return render(
    <Progress.Root value={40} data-testid="root" {...rootProps}>
      <Progress.Label data-testid="label">Uploading</Progress.Label>
      <Progress.Value data-testid="value" />
      <Progress.Track data-testid="track" className={trackClassName}>
        <Progress.Indicator data-testid="indicator" />
      </Progress.Track>
    </Progress.Root>,
  );
}

/** The indicator's inline `width`, e.g. "40%", or null while indeterminate. */
function indicatorWidth(container: HTMLElement): string | null {
  return part(container, "indicator").style.width || null;
}

describe("Progress", () => {
  describe("anatomy", () => {
    it("mirrors Base UI's five namespace members exactly", () => {
      expect(Object.keys(Progress).toSorted()).toEqual([
        "Indicator",
        "Label",
        "Root",
        "Track",
        "Value",
      ]);
    });

    it("renders a progressbar with the full ARIA value contract", async () => {
      const { container } = await renderProgress();

      const root = part(container, "root");
      expect(root.tagName).toBe("DIV");
      expect(root.getAttribute("role")).toBe("progressbar");
      expect(root.getAttribute("aria-valuemin")).toBe("0");
      expect(root.getAttribute("aria-valuemax")).toBe("100");
      expect(root.getAttribute("aria-valuenow")).toBe("40");
      expect(root.getAttribute("aria-valuetext")).toBe("40%");
    });

    it("names the bar through the label rather than a caller-owned id pair", async () => {
      const { container } = await renderProgress();

      const label = part(container, "label");
      expect(label.tagName).toBe("SPAN");
      expect(label.getAttribute("role")).toBe("presentation");
      expect(label.getAttribute("id")).not.toBeNull();
      expect(part(container, "root").getAttribute("aria-labelledby")).toBe(
        label.getAttribute("id"),
      );
    });

    it("hides the visible readout from screen readers", async () => {
      // The announced readout is the root's aria-valuetext; announcing the Value
      // span too would read the number twice.
      const { container } = await renderProgress();

      const value = part(container, "value");
      expect(value.tagName).toBe("SPAN");
      expect(value.getAttribute("aria-hidden")).toBe("true");
      expect(value.textContent).toBe("40%");
    });

    it("nests the indicator inside the track", async () => {
      const { container } = await renderProgress();

      expect(part(container, "indicator").parentElement).toBe(part(container, "track"));
    });

    it("honours a custom min/max on the ARIA contract", async () => {
      const { container } = await renderProgress({ value: 5, min: 0, max: 20 });

      const root = part(container, "root");
      expect(root.getAttribute("aria-valuemin")).toBe("0");
      expect(root.getAttribute("aria-valuemax")).toBe("20");
      expect(root.getAttribute("aria-valuenow")).toBe("5");
    });
  });

  describe("status", () => {
    it("marks a partial value data-progressing on every part", async () => {
      // Bare attributes, not data-status="progressing" — the recipes key off
      // `data-[indeterminate]:`, so pinning the real shape is what keeps them honest.
      const { container } = await renderProgress({ value: 40 });

      for (const testId of ["root", "label", "value", "track", "indicator"]) {
        const element = part(container, testId);
        expect(element.hasAttribute("data-progressing"), testId).toBe(true);
        expect(element.hasAttribute("data-complete"), testId).toBe(false);
        expect(element.hasAttribute("data-indeterminate"), testId).toBe(false);
      }
    });

    it("marks value === max data-complete", async () => {
      const { container } = await renderProgress({ value: 100 });

      const root = part(container, "root");
      expect(root.hasAttribute("data-complete")).toBe(true);
      expect(root.hasAttribute("data-progressing")).toBe(false);
      expect(part(container, "indicator").hasAttribute("data-complete")).toBe(true);
    });

    it("marks a null value data-indeterminate and drops aria-valuenow", async () => {
      const { container } = await renderProgress({ value: null });

      const root = part(container, "root");
      expect(root.hasAttribute("data-indeterminate")).toBe(true);
      expect(root.hasAttribute("data-progressing")).toBe(false);
      expect(root.hasAttribute("aria-valuenow")).toBe(false);
      expect(root.getAttribute("aria-valuetext")).toBe("indeterminate progress");
    });

    it("reaches data-complete against a custom max, not against 100", async () => {
      const { container } = await renderProgress({ value: 20, min: 0, max: 20 });

      expect(part(container, "root").hasAttribute("data-complete")).toBe(true);
    });
  });

  describe("indicator geometry", () => {
    it("is sized by Base UI's inline style, as a percentage of the range", async () => {
      const { container } = await renderProgress({ value: 40 });

      const indicator = part(container, "indicator");
      expect(indicatorWidth(container)).toBe("40%");
      expect(indicator.style.height).toBe("inherit");
      expect(indicator.style.insetInlineStart).toBe("0px");
    });

    it("measures the percentage against min/max, not against the raw value", async () => {
      // value=5 of 0..20 is 25% of the range. The Value part reads "5%" for the
      // same render — that divergence is Base UI's, and both halves are pinned.
      const { container } = await renderProgress({ value: 5, min: 0, max: 20 });

      expect(indicatorWidth(container)).toBe("25%");
      expect(part(container, "value").textContent).toBe("5%");
    });

    it("carries no inline width at all while indeterminate", async () => {
      // The one case a width utility in the recipe can win, which is exactly why
      // the recipe carries `data-[indeterminate]:w-full`.
      const { container } = await renderProgress({ value: null });

      expect(indicatorWidth(container)).toBeNull();
      expect(part(container, "indicator").getAttribute("style")).toBeNull();
    });
  });

  describe("value formatting", () => {
    it("renders nothing in the value part while indeterminate", async () => {
      const { container } = await renderProgress({ value: null });

      expect(part(container, "value").textContent).toBe("");
    });

    it("applies a caller's Intl format options to the readout", async () => {
      const { container } = await render(
        <Progress.Root value={0.4} format={{ style: "percent" }} locale="en-US" data-testid="root">
          <Progress.Value data-testid="value" />
        </Progress.Root>,
      );

      expect(part(container, "value").textContent).toBe("40%");
    });

    it("passes the formatted value and the raw value to a children function", async () => {
      const { container } = await render(
        <Progress.Root value={40} data-testid="root">
          <Progress.Value data-testid="value">
            {(formattedValue, value) => `${formattedValue} of 100 (raw ${value})`}
          </Progress.Value>
        </Progress.Root>,
      );

      expect(part(container, "value").textContent).toBe("40% of 100 (raw 40)");
    });

    it("hands the children function the string 'indeterminate' when there is no value", async () => {
      const { container } = await render(
        <Progress.Root value={null} data-testid="root">
          <Progress.Value data-testid="value">
            {(formattedValue) => `status: ${formattedValue}`}
          </Progress.Value>
        </Progress.Root>,
      );

      expect(part(container, "value").textContent).toBe("status: indeterminate");
    });

    it("lets a caller replace the announced text through getAriaValueText", async () => {
      const { container } = await renderProgress({
        value: 40,
        getAriaValueText: (formattedValue) => `${formattedValue} uploaded`,
      });

      expect(part(container, "root").getAttribute("aria-valuetext")).toBe("40% uploaded");
    });
  });

  describe("recipes", () => {
    it("paints all five parts", async () => {
      const { container } = await renderProgress();

      expect(classSet(part(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(part(container, "label"))).toEqual(LABEL_CLASSES.toSorted());
      expect(classSet(part(container, "value"))).toEqual(VALUE_CLASSES.toSorted());
      expect(classSet(part(container, "track"))).toEqual(TRACK_CLASSES.toSorted());
      expect(classSet(part(container, "indicator"))).toEqual(INDICATOR_CLASSES.toSorted());
    });

    it("paints every part the same in every status", async () => {
      // Status is `data-[…]:` modifiers inside one recipe per part, so the class
      // SET never varies with `value`.
      for (const value of [null, 40, 100]) {
        const { container } = await renderProgress({ value });

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
      const { container } = await renderProgress({ trackClassName: "h-3" });

      const track = part(container, "track");
      expect(track.classList.contains("h-3")).toBe(true);
      expect(track.classList.contains("h-2")).toBe(false);
      expect(track.classList.contains("rounded-full")).toBe(true);
      expect(track.classList.contains("bg-input")).toBe(true);
    });

    it("composes the root recipe onto another element through the render prop", async () => {
      const { container } = await render(
        <Progress.Root value={40} render={<section data-testid="root" />}>
          <Progress.Track data-testid="track" />
        </Progress.Root>,
      );

      const section = container.querySelector("section");
      expect(section).not.toBeNull();
      expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    });

    it("passes an app-owned aria-label and data-testid through to the root", async () => {
      const { container } = await render(
        <Progress.Root value={40} data-testid="upload-progress" aria-label="Upload progress">
          <Progress.Track />
        </Progress.Root>,
      );

      expect(part(container, "upload-progress").getAttribute("aria-label")).toBe("Upload progress");
    });
  });
});
