import type { Decorator } from "@storybook/react-vite";
import { type ReactElement, type ReactNode, useLayoutEffect } from "react";

import type { SchemeName } from "./scheme-assertions";

/*
 * Wallow-lrlm.11 — the ONE way a story selects a colour scheme.
 *
 * WHY THE CLASS GOES ON THE DOCUMENT ELEMENT. `renderThemeStyle` emits
 * `:root` / `.dark` / `.light` blocks carrying the RAW variables (`--background`,
 * `--sidebar`, …), while `styles.css`'s `@theme` declares the TOKEN
 * (`--color-background: var(--background, …)`) on `:root` ALONE. A `var()` inside
 * a custom property is substituted at computed-value time on the DECLARING
 * element, so a wrapper `<div className="dark">` rebinds the raw variable for its
 * descendants while the token above it keeps the light value it already computed
 * — and that stale value is what inherits down into every utility. The two blocks
 * only meet on one element: `document.documentElement`. Six story files used to
 * scope a scheme with a wrapper div and every story named *Dark among them
 * painted the LIGHT palette; `expectScheme` in `./scheme-assertions` now measures
 * that and would fail again if anyone went back to a wrapper.
 *
 * WHY THE CLEANUP IS LOAD-BEARING. All stories share one document, so a class
 * left on the root leaks into whatever renders next — and `renderThemeStyle`
 * emits `.light` AFTER `.dark`, so a leaked `.light` silently out-cascades a
 * later dark story at equal specificity. The cleanup below is what keeps a scheme
 * inside the story that asked for it. Leakage is not merely prevented, it is
 * CAUGHT: every scheme-scoped story asserts the palette it paints, in both
 * directions, so a decorator that forgot to clean up turns the next story red.
 */

/** Stamps `scheme` on the document element for as long as the story is mounted. */
function SchemeScope({
  scheme,
  children,
}: {
  scheme: SchemeName;
  children: ReactNode;
}): ReactElement {
  useLayoutEffect((): (() => void) => {
    const root: HTMLElement = document.documentElement;
    root.classList.add(scheme);

    return (): void => {
      root.classList.remove(scheme);
    };
  }, [scheme]);

  // The frame the story is read against — and the element `expectScheme` probes,
  // which is why it keeps `bg-background`. The scheme class is deliberately NOT
  // here: on a wrapper it would do nothing but look convincing.
  return <div className="bg-background text-foreground p-6">{children}</div>;
}

/** Renders the story in the fork's light scheme, stamped on the document element. */
export const lightScheme: Decorator = (Story) => (
  <SchemeScope scheme="light">
    <Story />
  </SchemeScope>
);

/** Renders the story in the fork's dark scheme, stamped on the document element. */
export const darkScheme: Decorator = (Story) => (
  <SchemeScope scheme="dark">
    <Story />
  </SchemeScope>
);
