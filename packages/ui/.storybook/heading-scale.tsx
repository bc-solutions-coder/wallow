import { expect, screen } from "storybook/test";
import type { ReactElement } from "react";

/*
 * Wallow-io5f — the assertion a title-bearing story makes about its own type
 * step: the heading it renders sits on the catalog-wide standard, 20px.
 *
 * WHY THIS EXISTS. The catalog spells a title with SIX recipes. Four of them —
 * `cardTitleRecipe`, `dialogTitleRecipe`, `alertDialogTitleRecipe`,
 * `drawerTitleRecipe` — are the "names the surface" kind and hard-coded
 * `text-lg` (18px) while `Text`'s `subheading` step, the other spelling of the
 * same thing, sat at `text-xl` (20px). This bead settles all four on 20px. The
 * remaining two are deliberately NOT in scope and must stay small:
 * `toastTitleRecipe` and `popoverTitleRecipe` are `text-sm` transient chrome,
 * not surface headings.
 *
 * WHY IT MEASURES. A `toHaveClass("text-xl")` assertion proves a string is
 * present, not that the element computes 20px: the live class list is the
 * `twMerge` of the recipe with whatever `className` a call site passed, so a
 * caller utility can win the size axis the recipe thought it owned while the
 * class-string assertion stays green over the wrong box.
 *
 * WHY IT CAN ONLY LIVE IN A STORY. Reading a computed font-size needs the real
 * Tailwind pipeline, and in `packages/ui` only the `storybook` Vitest project
 * has it; the `browser` project loads no CSS, so every probe there would compute
 * the same inherited size and every assertion below would pass vacuously.
 *
 * WHY `screen` AND NOT `canvasElement`. Dialog, AlertDialog and Drawer render
 * their popup through a Base UI portal, which lands outside the story canvas but
 * inside the document — so the title is only reachable from a document-scoped
 * query. The probes render in the canvas and are found the same way.
 *
 * WHY PROBES RATHER THAN A `20px` LITERAL. Asserting the number would bake
 * Tailwind's current `text-xl` into the catalog's own spec and go quietly wrong
 * for a fork that retunes its scale. Every claim below is a comparison against a
 * sibling carrying the utility the heading is supposed to land on, and the
 * probes double as the vacuity guard.
 */

/** The probe carrying the step a title must land on. */
const PROBE_XL = "heading-scale-probe-xl";

/** The probe carrying the step the four title recipes used to hard-code. */
const PROBE_LG = "heading-scale-probe-lg";

/** The probe carrying body copy, which a heading has to outrank. */
const PROBE_BODY = "heading-scale-probe-body";

/** How many distinct sizes the three probes must resolve to for a run to mean anything. */
const DISTINCT_STEPS = 3;

/**
 * The three type steps {@link expectHeadingScale} measures against, rendered
 * beside the story subject.
 *
 * Visually inert on purpose — `sr-only` keeps the probes out of the story's
 * screenshot without taking them out of layout, so they still compute a real
 * font size.
 */
export function HeadingScaleProbes(): ReactElement {
  return (
    <div className="sr-only">
      <div data-testid={PROBE_BODY} className="text-base">
        probe
      </div>
      <div data-testid={PROBE_LG} className="text-lg">
        probe
      </div>
      <div data-testid={PROBE_XL} className="text-xl">
        probe
      </div>
    </div>
  );
}

/** The computed `font-size` of the element carrying `testId`, anywhere in the document. */
async function fontSize(testId: string): Promise<string> {
  return getComputedStyle(await screen.findByTestId(testId)).fontSize;
}

/** A computed CSS length as a number — `Number("20px")` is `NaN`, so strip the unit. */
function px(length: string): number {
  return Number(length.replace("px", ""));
}

/**
 * A `play` that asserts the title carrying `titleTestId` sits on the
 * catalog-wide heading standard.
 *
 * Three claims, all relations rather than values: the title is the `text-xl`
 * step, it is NOT the `text-lg` step the recipe used to hard-code, and it is
 * strictly larger than body copy — the last being the reason the standard is
 * 20px and not 16px, since 16px is the browser's default body size and a heading
 * standardised there computes the same size as the copy beneath it.
 *
 * Requires the story to render {@link HeadingScaleProbes}.
 */
export function expectHeadingScale(titleTestId: string): () => Promise<void> {
  return async (): Promise<void> => {
    const body: string = await fontSize(PROBE_BODY);
    const lg: string = await fontSize(PROBE_LG);
    const xl: string = await fontSize(PROBE_XL);

    // The vacuity guard. With no stylesheet all three probes compute the
    // inherited size, and "the title matches text-xl" would also be "the title
    // matches text-lg".
    await expect(
      new Set([body, lg, xl]),
      "the Tailwind pipeline resolved all three steps",
    ).toHaveProperty("size", DISTINCT_STEPS);

    const title: string = await fontSize(titleTestId);

    await expect(title, `${titleTestId} renders at the text-xl step`).toBe(xl);
    await expect(title, `${titleTestId} left the text-lg step it hard-coded`).not.toBe(lg);
    await expect(
      px(title),
      `${titleTestId} at ${title} does not outrank body copy at ${body}`,
    ).toBeGreaterThan(px(body));
  };
}
