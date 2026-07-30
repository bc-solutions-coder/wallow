import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { listCardListRecipe, listCardRecipe } from "./list-card.styles";

/**
 * The card-wrapped list surface every wallow-web list page hand-rolls today:
 * a bordered card that clips its children, wrapping a divided `<ul>`.
 *
 * The catalog `Card` cannot play this role — its fixed padding fights rows that
 * must bleed to the card edge — so the surface is its own component, and its
 * one child is always the list.
 *
 * `className` is merged over the surface recipe, so a caller class always wins.
 */
export type ListCardProps = HTMLAttributes<HTMLDivElement> & {
  /**
   * The app's name for this list. The inner `<ul>`'s test id is DERIVED from
   * it (`{name}-table`), so an app names the list once and its E2E selectors
   * follow — the same rule packages/forms applies to a field catalog.
   */
  readonly name: string;
  /** Merged over the surface recipe, last value winning. */
  readonly className?: string;
};

export function ListCard({ name, className, children, ...rest }: ListCardProps): ReactElement {
  return (
    <div className={cn(listCardRecipe(), className)} {...rest}>
      <ul data-testid={`${name}-table`} className={listCardListRecipe()}>
        {children}
      </ul>
    </div>
  );
}
