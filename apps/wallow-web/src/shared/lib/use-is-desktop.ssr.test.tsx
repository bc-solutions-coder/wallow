import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { useIsDesktop } from "./use-is-desktop";

/**
 * `useIsDesktop`'s server snapshot — the one code path a browser-mode spec can
 * never reach, hence the `.ssr` name that routes this onto the node project.
 * `useSyncExternalStore` consults `getServerSnapshot` only when there is no
 * client store, so a spec that MOUNTS the hook takes `getSnapshot` and real
 * `matchMedia` on the first render and sees nothing wrong.
 *
 * The server has no viewport, so any boolean is a guess painted before
 * hydration can correct it. "Unknown" is the only honest answer.
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
    // On a phone this value is the first thing painted, so `true` paints the
    // desktop rail before hydration corrects it.
    expect(serverRenderedValue()).not.toBe("true");
  });

  it("does not claim the viewport is mobile either", () => {
    // A constant `false` only trades a flash on phones for one on laptops.
    expect(serverRenderedValue()).not.toBe("false");
  });

  it("reports the viewport as unknown", () => {
    // `undefined` means "no viewport observed yet", which is the truth on the
    // server and during React's first hydration render. Consumers render
    // viewport-neutral chrome while it holds.
    expect(serverRenderedValue()).toBe("undefined");
  });
});
