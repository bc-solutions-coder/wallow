import type { ReactElement } from "react";

/**
 * App-resolved theme CSS and the optional compiled stylesheet URL for the document head.
 */
export interface DocumentStylesProps {
  /**
   * Theme CSS prepared by the app, for example with renderThemeStyle from the styles package.
   * Supply trusted generated CSS, not request input.
   */
  readonly themeCss: string;
  /**
   * Compiled app stylesheet URL, or null to omit the link. Decide development versus production
   * behavior in the consuming app.
   */
  readonly stylesheetHref: string | null;
}

/**
 * Renders theme CSS and an optional stylesheet link in the document head. Supply
 * stylesheetHref from the app's own build environment; null omits the link.
 */
export function DocumentStyles({ themeCss, stylesheetHref }: DocumentStylesProps): ReactElement {
  return (
    <>
      <style>{themeCss}</style>
      {stylesheetHref === null ? null : <link rel="stylesheet" href={stylesheetHref} />}
    </>
  );
}
