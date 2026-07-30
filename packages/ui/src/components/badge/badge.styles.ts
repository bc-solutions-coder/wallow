import { cva, type VariantProps } from "class-variance-authority";

/**
 * The badge's class recipe. Style decisions live here and nowhere else — this
 * file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * The base string is the chip's SHAPE, which never varies: the inline pill that
 * six wallow-web surfaces (MfaSettingsSection, ProfileSection, OrganizationList,
 * AppList, InquiryList, InquiryDetail) hand-roll as the same literal class
 * string today. `neutral` reproduces that string byte-for-byte so those call
 * sites migrate onto the catalog without a visual diff.
 *
 * Each variant paints BOTH halves of a token pair. A variant that set only the
 * surface would leave the label at the inherited colour and fail contrast on its
 * own background.
 *
 * `warning` maps onto `primary` deliberately: the theme has no dedicated warning
 * token, and this fork's primary IS the amber `oklch(0.72 0.15 85)` — the only
 * warning-shaped colour the palette carries. Adding a sixth token belongs to the
 * styles package, not here, so warning spends the amber rather than inventing a
 * raw hue.
 */
export const badgeRecipe = cva("inline-block text-xs font-medium px-2.5 py-0.5 rounded-full", {
  variants: {
    variant: {
      neutral: "bg-accent text-accent-foreground",
      success: "bg-success text-success-foreground",
      warning: "bg-primary text-primary-foreground",
      destructive: "bg-destructive text-destructive-foreground",
    },
  },
  defaultVariants: { variant: "neutral" },
});

/** The recipe's variant props, mixed into `BadgeProps`. */
export type BadgeRecipeProps = VariantProps<typeof badgeRecipe>;
