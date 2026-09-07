import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { pageContainerRecipe } from "./page-container.styles";

/**
 * Div attributes for a centered page column; className can override the maximum width.
 */
export type PageContainerProps = HTMLAttributes<HTMLDivElement>;

/**
 * Constrains and centers page content. The surrounding app layout supplies navigation and
 * outer padding.
 */
export function PageContainer({ className, ...rest }: PageContainerProps): ReactElement {
  return <div {...rest} className={cn(pageContainerRecipe(), className)} />;
}
