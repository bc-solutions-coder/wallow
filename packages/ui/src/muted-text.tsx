import type { HTMLAttributes, ReactElement } from "react";

/**
 * The shared muted paragraph. Sourced from 41x `text-sm text-muted-foreground`
 * in wallow-auth — the strongest single recipe in the inventory. Renders a `<p>`;
 * children, className, and data-testid pass through (a caller `className` is
 * appended to the recipe).
 */
export type MutedTextProps = HTMLAttributes<HTMLParagraphElement>;

export function MutedText({ className, ...rest }: MutedTextProps): ReactElement {
  const recipe = "text-sm text-muted-foreground";
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <p className={merged} {...rest} />;
}
