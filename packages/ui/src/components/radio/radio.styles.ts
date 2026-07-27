import { cva } from "class-variance-authority";

/**
 * The radio's class recipes — one per Base UI part, no JSX and no React import,
 * so the styling can be read and diffed without the component around it.
 *
 * Every utility must be a semantic token class from `@bc-solutions-coder/styles`
 * (`border-input`, `bg-background`, `bg-primary`, `bg-primary-foreground`); no
 * raw colour values. Selected/disabled treatment hangs off Base UI's
 * `data-checked` / `data-unchecked` / `data-disabled` state attributes rather
 * than CSS pseudo-classes, so it survives the `render` prop swapping the
 * element out.
 */
export const radioRootRecipe = cva(
  "inline-flex size-4 shrink-0 items-center justify-center rounded-full border border-input bg-background data-[checked]:border-primary data-[checked]:bg-primary data-[disabled]:opacity-50",
);

/**
 * The dot inside the root. Base UI unmounts the indicator entirely while the
 * radio is unselected, so the `data-unchecked` rule only bites for callers that
 * pass `keepMounted`.
 */
export const radioIndicatorRecipe = cva(
  "size-2 rounded-full bg-primary-foreground data-[unchecked]:hidden",
);
