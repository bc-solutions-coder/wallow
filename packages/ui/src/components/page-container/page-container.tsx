import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { pageContainerRecipe } from "./page-container.styles";

/**
 * The column a page body sits in: one width rule for every page, applied by the
 * page rather than guessed at.
 *
 * `PageHeader`'s sibling — a page opens with the header and wraps the whole body
 * in this. Everything outside it (nav rail, main column, padding) belongs to the
 * app's layout route, so this component adds nothing but width and centring, and
 * a page writes no `max-w-*` utility of its own.
 */
export type PageContainerProps = HTMLAttributes<HTMLDivElement>;

export function PageContainer({ className, ...rest }: PageContainerProps): ReactElement {
  return <div {...rest} className={cn(pageContainerRecipe(), className)} />;
}
