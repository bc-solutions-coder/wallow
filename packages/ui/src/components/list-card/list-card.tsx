import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { listCardListRecipe, listCardRecipe } from "./list-card.styles";

/**
 * Div attributes for a card containing a divided list. Pass list items as children; name
 * identifies the inner list for tests.
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

/**
 * Renders a bordered card containing a divided ul. Its name produces the inner list's
 * data-testid as `${name}-table`.
 */
export function ListCard({ name, className, children, ...rest }: ListCardProps): ReactElement {
  return (
    <div className={cn(listCardRecipe(), className)} {...rest}>
      <ul data-testid={`${name}-table`} className={listCardListRecipe()}>
        {children}
      </ul>
    </div>
  );
}
