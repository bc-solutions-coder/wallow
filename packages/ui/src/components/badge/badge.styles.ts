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
 * `warning` spent `primary` until the styles package grew a real warning token —
 * this fork's primary IS an amber, so it was the only warning-shaped colour the
 * palette carried, and adding a sixth token was explicitly F1's job rather than
 * this component's. That token now exists and falls back to `--primary`, so this
 * arm keeps the same colour on this fork while a fork whose primary is not amber
 * finally gets a warning chip that reads as one.
 */
export const badgeRecipe = cva("inline-block text-xs font-medium px-2.5 py-0.5 rounded-full", {
  variants: {
    variant: {
      neutral: "bg-accent text-accent-foreground",
      success: "bg-success text-success-foreground",
      warning: "bg-warning text-warning-foreground",
      destructive: "bg-destructive text-destructive-foreground",
    },
  },
  defaultVariants: { variant: "neutral" },
});

/** The recipe's variant props, mixed into `BadgeProps`. */
export type BadgeRecipeProps = VariantProps<typeof badgeRecipe>;
