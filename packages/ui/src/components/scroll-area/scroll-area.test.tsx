import { render } from "@bc-solutions-coder/testing/render";
import type { CSSProperties, ReactNode } from "react";
import { describe, expect, it } from "vitest";

import { ScrollArea } from "./scroll-area";

/*
 * Wallow-m5aq.4.5 — Scroll Area. Same spec shape as the Wave-1 exemplar
 * (Wallow-m5aq.2.1) and the Wave-2 exemplar (Wallow-m5aq.3.1): browser vitest
 * project, nothing mocked, the recipes asserted THROUGH the component, class
 * assertions as an order-free set.
 *
 * Nothing here is portalled and nothing here is a popup, so every query goes
 * through render()'s `container` and none of the Wave-2 overlay gotchas apply.
 *
 * ANATOMY, measured against the installed Base UI 1.6.0 rather than read off the
 * docs (a throwaway probe spec, since deleted):
 *
 *   <div role="presentation" style="position:relative; --scroll-area-corner-*">   <- Root
 *     <div role="presentation" class="base-ui-disable-scrollbar"
 *          tabindex style="overflow:scroll; --scroll-area-overflow-*">            <- Viewport
 *       <div role="presentation" style="min-width:fit-content">                   <- Content
 *     <div data-orientation data-id style="position:absolute;
 *          --scroll-area-thumb-height">                                           <- Scrollbar
 *       <div data-orientation style="height:var(--scroll-area-thumb-height);
 *            transform:translate3d(…)">                                           <- Thumb
 *     <div style="position:absolute; width; height">                              <- Corner
 *
 * Five measurements are worth stating, because each is easy to assume wrong:
 *   - THE VIEWPORT IS THE ONE PART IN THE WHOLE CATALOG THAT CARRIES A BASE UI
 *     CLASS OF ITS OWN. Base UI merges `base-ui-disable-scrollbar` (the rule that
 *     hides the native scrollbars) into the viewport's className, so the
 *     viewport's class set is that class PLUS the recipe — not pure recipe like
 *     every other part in the catalog. A spec that asserted a pure recipe set
 *     here would force an implementation that DROPS Base UI's class and lets the
 *     native scrollbars show through.
 *   - A SCROLLBAR FOR A NON-OVERFLOWING AXIS IS NOT RENDERED AT ALL. Base UI
 *     measures the viewport and mounts each track only when its own axis
 *     overflows, unless the track opts into `keepMounted`. That measurement runs
 *     in a layout effect + microtask, so scrollbar presence is polled, never read
 *     synchronously off the first render.
 *   - THE BROWSER PROJECT COMPILES NO TAILWIND, so `size-full` and friends are
 *     inert here and NOTHING would overflow. Every fixture therefore sets real
 *     pixel dimensions inline (the TEST_BOX precedent from checkbox.test.tsx) —
 *     without them Base UI measures a 0-high viewport, decides there is no
 *     overflow, and no scrollbar ever mounts.
 *   - `data-scrolling` REQUIRES A USER-DRIVEN SCROLL. The viewport starts each
 *     scroll assumed programmatic; a wheel/pointer/key event flips that flag, and
 *     only then does a scroll publish `data-scrolling`. Hence the wheel-then-
 *     scrollTo order in `scrollViewport()`. The flag also CLEARS 500ms later, so
 *     it is polled for arrival, never asserted after a fixed wait.
 *   - `data-hovering` IS DELIBERATELY NOT ASSERTED ANYWHERE BELOW. It tracks the
 *     real pointer, and the recorded packages/ui gotcha is that the mouse
 *     position persists across specs within a file, so a hover assertion here
 *     would pass or fail depending on what the previous spec clicked. The
 *     scrollbar recipe is therefore NOT allowed to gate its visibility on
 *     `data-[hovering]:` — a track that only appears on hover cannot be proven.
 */

/** Every utility `ScrollArea.Root` must render. Single source of truth. */
const ROOT_CLASSES = ["relative", "overflow-hidden", "rounded-md", "bg-card"];

/**
 * Every utility `ScrollArea.Viewport` must render, ON TOP of Base UI's own
 * `base-ui-disable-scrollbar`. The viewport is a tab stop whenever it scrolls,
 * which is why it owns the focus ring.
 */
const VIEWPORT_CLASSES = [
  "size-full",
  "rounded-[inherit]",
  "outline-none",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
];

/** The class Base UI itself merges into the viewport. Never ours to drop. */
const BASE_UI_VIEWPORT_CLASS = "base-ui-disable-scrollbar";

/** Every utility `ScrollArea.Content` must render. */
const CONTENT_CLASSES = ["text-sm", "text-foreground"];

/**
 * Every utility `ScrollArea.Scrollbar` must render. ONE recipe paints both
 * tracks: the axis arrives as Base UI's `data-orientation`, so the two
 * thicknesses are `data-[orientation=…]:` modifiers rather than a cva variant a
 * caller would have to keep in step with the `orientation` prop.
 */
const SCROLLBAR_CLASSES = [
  "flex",
  "rounded-full",
  "bg-muted",
  "transition-colors",
  "duration-150",
  "data-[orientation=vertical]:w-2",
  "data-[orientation=horizontal]:h-2",
];

/**
 * Every utility `ScrollArea.Thumb` must render. The cross-axis `*-full` pair is
 * what makes the handle fill its track: Base UI sizes the thumb only ALONG the
 * scroll axis, inline, from the track's `--scroll-area-thumb-*` custom property.
 */
const THUMB_CLASSES = [
  "rounded-full",
  "bg-border",
  "transition-colors",
  "hover:bg-muted-foreground",
  "data-[orientation=vertical]:w-full",
  "data-[orientation=horizontal]:h-full",
];

/** Every utility `ScrollArea.Corner` must render. Base UI sizes it inline. */
const CORNER_CLASSES = ["bg-muted"];

/**
 * Real pixel dimensions, because the browser project compiles no Tailwind. Same
 * device as `TEST_BOX` in checkbox.test.tsx.
 */
const AREA_BOX: CSSProperties = { width: 120, height: 80 };

/** Bigger than AREA_BOX on both axes, so both tracks have something to do. */
const OVERFLOWING_BOX: CSSProperties = { width: 400, height: 400 };

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function part(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

function maybePart(container: HTMLElement, testId: string): HTMLElement | null {
  return container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

interface FixtureProps {
  /** Inline size of the content, so a test can choose which axes overflow. */
  readonly contentBox?: CSSProperties;
  /** Keeps both tracks mounted even on an axis that does not overflow. */
  readonly keepMounted?: boolean;
  /** Applied to `ScrollArea.Content`, for the tailwind-merge proof. */
  readonly contentClassName?: string;
  /** Applied to `ScrollArea.Viewport`, for the Base-UI-class merge proof. */
  readonly viewportClassName?: string;
  /** Extra parts to render inside the root. */
  readonly children?: ReactNode;
}

/**
 * The fixture every case below starts from: a 120x80 window onto a 400x400 box,
 * so BOTH axes overflow and both tracks mount.
 */
function renderScrollArea({
  contentBox = OVERFLOWING_BOX,
  keepMounted,
  contentClassName,
  viewportClassName,
}: FixtureProps = {}) {
  return render(
    <ScrollArea.Root data-testid="root" style={AREA_BOX}>
      <ScrollArea.Viewport data-testid="viewport" style={AREA_BOX} className={viewportClassName}>
        <ScrollArea.Content data-testid="content" className={contentClassName}>
          <div style={contentBox}>Long enough to overflow</div>
        </ScrollArea.Content>
      </ScrollArea.Viewport>
      <ScrollArea.Scrollbar
        data-testid="scrollbar-y"
        orientation="vertical"
        keepMounted={keepMounted}
      >
        <ScrollArea.Thumb data-testid="thumb-y" />
      </ScrollArea.Scrollbar>
      <ScrollArea.Scrollbar
        data-testid="scrollbar-x"
        orientation="horizontal"
        keepMounted={keepMounted}
      >
        <ScrollArea.Thumb data-testid="thumb-x" />
      </ScrollArea.Scrollbar>
      <ScrollArea.Corner data-testid="corner" />
    </ScrollArea.Root>,
  );
}

/** Waits for Base UI's overflow measurement to mount the tracks. */
async function waitForScrollbars(container: HTMLElement): Promise<void> {
  await expect.poll(() => maybePart(container, "scrollbar-y")).not.toBeNull();
  await expect.poll(() => maybePart(container, "scrollbar-x")).not.toBeNull();
}

/**
 * Scrolls the viewport the way a USER would, as far as Base UI is concerned: the
 * wheel event is what clears the "this scroll was programmatic" flag, so it has
 * to be dispatched before the scroll position moves.
 */
function scrollViewport(viewport: HTMLElement, top: number): void {
  viewport.dispatchEvent(new WheelEvent("wheel", { deltaY: top, bubbles: true }));
  viewport.scrollTo({ top });
}

describe("ScrollArea", () => {
  describe("anatomy", () => {
    it("mirrors Base UI's six namespace members exactly", () => {
      expect(Object.keys(ScrollArea).toSorted()).toEqual([
        "Content",
        "Corner",
        "Root",
        "Scrollbar",
        "Thumb",
        "Viewport",
      ]);
    });

    it("renders the root, viewport and content as presentational divs", async () => {
      const { container } = await renderScrollArea();

      for (const testId of ["root", "viewport", "content"]) {
        expect(part(container, testId).tagName, testId).toBe("DIV");
        expect(part(container, testId).getAttribute("role"), testId).toBe("presentation");
      }
    });

    it("mounts a track per overflowing axis, each carrying its orientation", async () => {
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);

      expect(part(container, "scrollbar-y").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "scrollbar-x").getAttribute("data-orientation")).toBe("horizontal");
      expect(part(container, "thumb-y").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "thumb-x").getAttribute("data-orientation")).toBe("horizontal");
    });

    it("publishes the overflow state every recipe keys off", async () => {
      const { container } = await renderScrollArea();
      await expect
        .poll(() => part(container, "root").hasAttribute("data-has-overflow-y"))
        .toBe(true);

      for (const testId of ["root", "viewport", "content"]) {
        expect(part(container, testId).hasAttribute("data-has-overflow-x"), testId).toBe(true);
        expect(part(container, testId).hasAttribute("data-has-overflow-y"), testId).toBe(true);
        expect(part(container, testId).hasAttribute("data-overflow-x-end"), testId).toBe(true);
        expect(part(container, testId).hasAttribute("data-overflow-y-end"), testId).toBe(true);
        // Not yet scrolled, so neither START edge is past its threshold.
        expect(part(container, testId).hasAttribute("data-overflow-y-start"), testId).toBe(false);
      }
    });

    it("puts a scrollable viewport in the tab order", async () => {
      // A scrollable region has to be reachable by keyboard.
      const { container } = await renderScrollArea();

      await expect.poll(() => part(container, "viewport").getAttribute("tabindex")).toBe("0");
    });

    it("leaves an unscrollable viewport out of the tab order", async () => {
      // ...and an unscrollable one must not become a dead tab stop.
      const { container } = await renderScrollArea({ contentBox: { width: 10, height: 10 } });

      expect(part(container, "viewport").getAttribute("tabindex")).toBe("-1");
    });

    it("does not render a track for an axis that does not overflow", async () => {
      // Tall but not wide: the vertical track mounts, the horizontal one never
      // exists at all — the recipe styles a part that is legitimately absent.
      const { container } = await renderScrollArea({ contentBox: { width: 10, height: 400 } });
      await expect.poll(() => maybePart(container, "scrollbar-y")).not.toBeNull();

      expect(maybePart(container, "scrollbar-x")).toBeNull();
      expect(maybePart(container, "thumb-x")).toBeNull();
    });

    it("keeps a track for a non-overflowing axis under keepMounted", async () => {
      const { container } = await renderScrollArea({
        contentBox: { width: 10, height: 400 },
        keepMounted: true,
      });

      await expect.poll(() => maybePart(container, "scrollbar-x")).not.toBeNull();
      expect(part(container, "scrollbar-x").getAttribute("data-orientation")).toBe("horizontal");
    });
  });

  describe("recipes", () => {
    it("paints the root, the content and the corner", async () => {
      const { container } = await renderScrollArea();

      expect(classSet(part(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(part(container, "content"))).toEqual(CONTENT_CLASSES.toSorted());
      expect(classSet(part(container, "corner"))).toEqual(CORNER_CLASSES.toSorted());
    });

    it("adds the viewport recipe ON TOP of Base UI's scrollbar-hiding class", async () => {
      // The one part in the catalog whose class set is not pure recipe. Dropping
      // Base UI's class here would let the native scrollbars show through beside
      // the styled ones.
      const { container } = await renderScrollArea();

      expect(classSet(part(container, "viewport"))).toEqual(
        [...VIEWPORT_CLASSES, BASE_UI_VIEWPORT_CLASS].toSorted(),
      );
    });

    it("paints both tracks from the one scrollbar recipe", async () => {
      // Vertical and horizontal differ only by `data-orientation`, so the class
      // SET is identical on both and the axis is a modifier inside it.
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);

      for (const testId of ["scrollbar-y", "scrollbar-x"]) {
        expect(classSet(part(container, testId)), testId).toEqual(SCROLLBAR_CLASSES.toSorted());
      }
    });

    it("paints both thumbs from the one thumb recipe", async () => {
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);

      for (const testId of ["thumb-y", "thumb-x"]) {
        expect(classSet(part(container, testId)), testId).toEqual(THUMB_CLASSES.toSorted());
      }
    });
  });

  describe("thumb geometry", () => {
    it("takes its along-axis size from the track's custom property", async () => {
      // The whole point of the part: Base UI measures the viewport and writes
      // `--scroll-area-thumb-*` onto the TRACK, and sizes the thumb from it
      // inline. The recipe may only supply the CROSS-axis size, or the handle
      // stops tracking how much content there is.
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);

      expect(
        part(container, "scrollbar-y").style.getPropertyValue("--scroll-area-thumb-height"),
      ).not.toBe("");
      expect(
        part(container, "scrollbar-x").style.getPropertyValue("--scroll-area-thumb-width"),
      ).not.toBe("");
      expect(part(container, "thumb-y").style.height).toBe("var(--scroll-area-thumb-height)");
      expect(part(container, "thumb-x").style.width).toBe("var(--scroll-area-thumb-width)");
    });

    it("moves the thumb as the viewport scrolls", async () => {
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);
      expect(part(container, "thumb-y").style.transform).toBe("translate3d(0px, 0px, 0px)");

      scrollViewport(part(container, "viewport"), 120);

      await expect
        .poll(() => part(container, "thumb-y").style.transform)
        .not.toBe("translate3d(0px, 0px, 0px)");
    });
  });

  describe("scrolling", () => {
    it("flags the parts data-scrolling while a user scroll is in flight", async () => {
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);
      expect(part(container, "viewport").hasAttribute("data-scrolling")).toBe(false);

      scrollViewport(part(container, "viewport"), 120);

      await expect
        .poll(() => part(container, "viewport").hasAttribute("data-scrolling"))
        .toBe(true);
      expect(part(container, "root").hasAttribute("data-scrolling")).toBe(true);
      expect(part(container, "thumb-y").hasAttribute("data-scrolling")).toBe(true);
    });

    it("publishes the start-edge attribute once scrolled away from the top", async () => {
      const { container } = await renderScrollArea();
      await waitForScrollbars(container);

      scrollViewport(part(container, "viewport"), 120);

      await expect
        .poll(() => part(container, "viewport").hasAttribute("data-overflow-y-start"))
        .toBe(true);
      expect(
        part(container, "viewport").style.getPropertyValue("--scroll-area-overflow-y-start"),
      ).toBe("120px");
    });
  });

  describe("composition", () => {
    it("lets a caller className override a recipe utility on the content", async () => {
      // The cn()/tailwind-merge proof for this component: the conflicting
      // font-size utility is REMOVED rather than appended after, while the
      // utilities the caller never mentioned survive.
      const { container } = await renderScrollArea({ contentClassName: "text-base" });

      const content = part(container, "content");
      expect(content.classList.contains("text-base")).toBe(true);
      expect(content.classList.contains("text-sm")).toBe(false);
      expect(content.classList.contains("text-foreground")).toBe(true);
    });

    it("merges a caller className with Base UI's class rather than replacing it", async () => {
      const { container } = await renderScrollArea({ viewportClassName: "rounded-none" });

      const viewport = part(container, "viewport");
      expect(viewport.classList.contains(BASE_UI_VIEWPORT_CLASS)).toBe(true);
      expect(viewport.classList.contains("rounded-none")).toBe(true);
      expect(viewport.classList.contains("rounded-[inherit]")).toBe(false);
      expect(viewport.classList.contains("outline-none")).toBe(true);
    });

    it("composes the root recipe onto another element through the render prop", async () => {
      const { container } = await render(
        <ScrollArea.Root render={<section data-testid="root" />} style={AREA_BOX}>
          <ScrollArea.Viewport data-testid="viewport" style={AREA_BOX}>
            <ScrollArea.Content>
              <div style={OVERFLOWING_BOX}>Long enough to overflow</div>
            </ScrollArea.Content>
          </ScrollArea.Viewport>
        </ScrollArea.Root>,
      );

      const section = container.querySelector("section");
      expect(section).not.toBeNull();
      expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    });

    it("passes an app-owned aria-label and data-testid through to the viewport", async () => {
      const { container } = await render(
        <ScrollArea.Root style={AREA_BOX}>
          <ScrollArea.Viewport
            data-testid="changelog-viewport"
            aria-label="Changelog"
            style={AREA_BOX}
          >
            <ScrollArea.Content>
              <div style={OVERFLOWING_BOX}>Long enough to overflow</div>
            </ScrollArea.Content>
          </ScrollArea.Viewport>
        </ScrollArea.Root>,
      );

      expect(part(container, "changelog-viewport").getAttribute("aria-label")).toBe("Changelog");
    });
  });
});
