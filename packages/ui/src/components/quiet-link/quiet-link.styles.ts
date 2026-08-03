import { cva, type VariantProps } from "class-variance-authority";

/**
 * The muted secondary link. Sourced from 13 hand-spelled anchors across both
 * apps — card footers ("Back to sign in"), "Forgot password?", "Skip for now",
 * and wallow-web's inline back-links.
 *
 * Nine of those sites spelled the hover `text-foreground` and two spelled it
 * `text-primary`; the dominant one is the standard, because a link that recedes
 * at rest should resolve toward the body colour rather than pick up the accent
 * an action link already owns (`buttonRecipe`'s `link` arm).
 */
export const quietLinkRecipe = cva("text-sm text-muted-foreground hover:text-foreground");

export type QuietLinkRecipeProps = VariantProps<typeof quietLinkRecipe>;
