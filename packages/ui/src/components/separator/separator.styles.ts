import { cva, type VariantProps } from "class-variance-authority";

/**
 * The separator — Base UI's `Separator`, a `<div role="separator">` that carries
 * `data-orientation="horizontal" | "vertical"`.
 *
 * No cva variant: orientation is a PROP OF THE BASE UI PART, not of this recipe,
 * and Base UI publishes it as a `data-*` attribute. Both treatments therefore
 * belong in the base string as `data-[orientation=...]:` modifiers — one rule
 * pair sets the hairline's thickness and the other its length — rather than as a
 * cva variant a caller would have to keep in step with the `orientation` prop by
 * hand. `VariantProps` is still exported so the part keeps the catalog-wide
 * props shape and a later variant axis stays a non-breaking addition.
 *
 * Every utility must be a semantic token class from `@bc-solutions-coder/styles`
 * (`bg-border`); no raw colour values.
 */
export const separatorRecipe = cva(
  "shrink-0 bg-border data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-full data-[orientation=vertical]:h-full data-[orientation=vertical]:w-px",
);

/** The recipe's variant props, mixed into `SeparatorProps`. */
export type SeparatorRecipeProps = VariantProps<typeof separatorRecipe>;
