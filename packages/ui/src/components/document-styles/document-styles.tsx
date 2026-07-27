import type { ReactElement } from "react";

export interface DocumentStylesProps {
  /**
   * The fork's resolved theme, already serialized to a CSS string by the app
   * (i.e. `renderThemeStyle(branding)` output). It is passed in as data rather
   * than computed here so packages/ui never imports `@bc-solutions-coder/styles`
   * nor an app-local `../lib/branding` — the same props-only rule
   * `fork-attribution.tsx` follows. The string is a plain text child (React
   * escapes nothing into it and no markup is interpolated); it is generated
   * from `api/branding.json` at build time, never from request input.
   */
  readonly themeCss: string;
  /**
   * The compiled stylesheet href, or `null` when none should be linked.
   * REQUIRED with no default — a deliberate drift guard so a consuming app
   * cannot forget it and still compile. The dev/prod choice
   * (`import.meta.env.DEV ? null : "/client.css"`) must be made in the app
   * shell and passed in, never read via `import.meta.env` inside this library:
   * packages/ui ships a prebuilt dist bundle, so that check would bake in the
   * library's own build env instead of the consuming app's.
   */
  readonly stylesheetHref: string | null;
}

/**
 * The document `<head>` theme + stylesheet delivery block shared by both apps'
 * `__root.tsx`: the fork's theme CSS in a `<style>`, and — when a non-null
 * `stylesheetHref` is given — a `<link rel="stylesheet">` to the compiled entry
 * CSS. Centralizing it here is the structural fix for wallow-web's missing
 * `/client.css` link (Wallow-w6s6.3).
 */
export function DocumentStyles({ themeCss, stylesheetHref }: DocumentStylesProps): ReactElement {
  return (
    <>
      <style>{themeCss}</style>
      {stylesheetHref === null ? null : <link rel="stylesheet" href={stylesheetHref} />}
    </>
  );
}
