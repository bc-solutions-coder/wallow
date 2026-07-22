import { useEffect } from "react";

/**
 * The attribute E2E tests wait on to know the React app has hydrated. This is the
 * direct replacement for Blazor's `data-blazor-ready`, which
 * `Components/Shared/BlazorReadyIndicator.razor` set once the SignalR circuit was
 * up. Shared across every fork frontend (auth + web) via `@bc-solutions-coder/ui`.
 */
export const READY_ATTRIBUTE = "data-app-ready";

/**
 * Signals that the host app has hydrated and is interactive.
 *
 * Renders nothing. Instead it stamps `data-app-ready="true"` (and the app's
 * `testId`) onto `document.body` from an effect — deliberately mirroring
 * `BlazorReadyIndicator.razor`, which calls
 * `document.body.setAttribute("data-blazor-ready", "true")` from
 * `OnAfterRenderAsync(firstRender)`. `useEffect` is React's equivalent moment:
 * it does not run during SSR and does not run during hydration's render pass,
 * only after the tree is committed and interactive — so the attribute's presence
 * means the same thing Blazor's did. A test that finds it can trust that event
 * handlers are attached, which is exactly what
 * `WaitForBlazorReadyAsync`/`[data-blazor-ready='true']` bought the E2E suite.
 *
 * Writing to `document.body` rather than rendering a marker element is what
 * keeps this hydration-safe: the server emits no such attribute, so rendering
 * one conditionally would be a hydration mismatch. A post-commit DOM mutation
 * has no server counterpart to disagree with.
 *
 * `testId` is a required prop — the only divergence the two per-app copies
 * previously hard-coded (auth passes `auth-ready`, web passes `web-ready`).
 */
export function ReadyIndicator({ testId }: { testId: string }): null {
  useEffect((): (() => void) => {
    // `dataset.appReady`/`dataset.testid` are the `data-app-ready`/`data-testid`
    // attributes named above — the tests assert the rendered attribute selectors
    // themselves, so the E2E contract is pinned rather than implied.
    document.body.dataset.testid = testId;
    document.body.dataset.appReady = "true";

    return (): void => {
      delete document.body.dataset.appReady;
      delete document.body.dataset.testid;
    };
  }, [testId]);

  return null;
}
