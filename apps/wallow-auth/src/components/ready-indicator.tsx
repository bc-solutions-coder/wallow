import { useEffect } from "react";

/**
 * The attribute E2E tests wait on to know the React app has hydrated
 * (Wallow-vec7.1.5). This is the direct replacement for Blazor's
 * `data-blazor-ready`, which `Components/Shared/BlazorReadyIndicator.razor` sets
 * once the SignalR circuit is up.
 */
export const READY_ATTRIBUTE = "data-app-ready";

/** The `data-testid` the ready element carries, per the repo's E2E selector rules. */
export const READY_TEST_ID = "auth-ready";

/**
 * Signals that the auth app has hydrated and is interactive.
 *
 * Renders nothing. Instead it stamps `data-app-ready="true"` (and the
 * `auth-ready` test id) onto `document.body` from an effect — deliberately
 * mirroring `BlazorReadyIndicator.razor`, which calls
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
 */
export function ReadyIndicator(): null {
  useEffect((): (() => void) => {
    // `dataset.appReady`/`dataset.testid` are the `data-app-ready`/`data-testid`
    // attributes named above — the tests assert the rendered attribute selectors
    // themselves, so the E2E contract is pinned rather than implied.
    document.body.dataset.testid = READY_TEST_ID;
    document.body.dataset.appReady = "true";

    return (): void => {
      delete document.body.dataset.appReady;
      delete document.body.dataset.testid;
    };
  }, []);

  return null;
}
