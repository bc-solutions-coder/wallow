import { cva } from "class-variance-authority";

/**
 * The switch track: the pill the thumb slides inside, and the part that carries
 * the on/off colour.
 *
 * The track's size is not decoration — it IS the component. A switch has no
 * text and no intrinsic box, so `h-6 w-11` is the only thing giving it a hit
 * area. State hangs off Base UI's `data-checked`/`data-disabled` attributes
 * rather than CSS pseudo-classes, so the styling survives a `render`-prop
 * substitution onto a non-input element.
 *
 * The four size utilities across both recipes are one measurement, not four
 * choices: `h-6` (24px) and `w-11` (44px) with a `p-0.5` (2px) inset leave a
 * 20px inner box, which is the thumb's `size-5`, and the thumb's travel is
 * 44 - 4 - 20 = 20px = `translate-x-5`. Changing one requires changing all four.
 */
export const switchRootRecipe = cva(
  "inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full bg-input p-0.5 transition-colors data-[checked]:bg-primary data-[disabled]:opacity-50",
);

/**
 * The thumb: the knob that slides. It reads the SAME state attributes as the
 * track, because Base UI's `Switch.Thumb` re-publishes the root's state onto
 * itself through context — which is what lets the travel distance
 * (`data-[checked]:translate-x-*`) live here rather than in a parent selector.
 */
export const switchThumbRecipe = cva(
  "block size-5 rounded-full bg-background transition-transform data-[checked]:translate-x-5",
);
