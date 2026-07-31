import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { useIsDesktop } from "./use-is-desktop";

/**
 * Server-snapshot contract for `useIsDesktop` (Wallow-lrlm.6.3).
 *
 * A node-project spec by design (listed in `vitest.config.ts`'s `nodeTsxSpecs`):
 * `getServerSnapshot` is the ONE code path a browser-mode spec can never reach.
 * `useSyncExternalStore` consults it only when there is no client store to read
 * — during `renderToString` on the server, and again for React's first
 * hydration render — so a spec that MOUNTS the hook takes `getSnapshot` and real
 * `matchMedia` on the very first render and sees nothing wrong. `renderToString`
 * is the repo's existing way of asking "what does the server actually emit"
 * (`src/app/routes/index.test.tsx`).
 *
 * The bug: the server has no viewport, so any boolean it returns is a guess, and
 * the guess is painted before hydration can correct it. Returning `true` makes
 * every phone paint the desktop rail first. Returning `false` would only move
 * the flash onto laptops. Both are asserted against here — the fix is to answer
 * "unknown", not to answer differently.
 */

/** Renders the hook's value into an attribute so `renderToString` can be read. */
function IsDesktopProbe() {
  const isDesktop: boolean | undefined = useIsDesktop();
  return <span data-testid="is-desktop-probe" data-value={String(isDesktop)} />;
}

function serverRenderedValue(): string {
  const html: string = renderToString(<IsDesktopProbe />);
  const match: RegExpMatchArray | null = html.match(/data-value="([^"]*)"/u);
  expect(match, `probe emitted no data-value attribute: ${html}`).not.toBeNull();
  return match![1];
}

describe("useIsDesktop server snapshot", () => {
  it("does not claim the viewport is desktop", () => {
    // The current defect, stated directly: on a phone this value is the first
    // thing painted, and it is wrong.
    expect(serverRenderedValue()).not.toBe("true");
  });

  it("does not claim the viewport is mobile either", () => {
    // Guards the lazy fix. Flipping the constant to `false` trades a flash on
    // phones for a flash on laptops; the server still has no viewport.
    expect(serverRenderedValue()).not.toBe("false");
  });

  it("reports the viewport as unknown", () => {
    // The positive contract: `undefined` is "no viewport has been observed yet",
    // which is the truth on the server and during React's first hydration
    // render. Consumers are expected to render viewport-neutral chrome while it
    // holds; see `DashboardLayout.ssr-flash.test.tsx` for that half.
    expect(serverRenderedValue()).toBe("undefined");
  });
});
