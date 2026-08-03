/**
 * The full-page hand-off guard a browser project installs once, in its setup file.
 *
 * A spec that lets a real navigation reach the Vitest iframe does not fail: the
 * runner loses the iframe and dies mid-file, and the error names whichever file
 * the orchestrator was loading next. So the report both misattributes and
 * over-reports, and the leak reads as an intermittent flake in a neighbour.
 *
 * `location` is [Unforgeable] in real Chromium, so nothing can shadow it. The
 * Navigation API's `navigate` event is the one seam from which a page can observe
 * and veto a hand-off, and it fires for every source — an anchor whose default
 * nobody prevented, `location.assign`, a form submit. That is why this is
 * installed once per project rather than repaired once per router stub: the
 * construct that navigates does not have to be known, or reachable, to be caught.
 *
 * Only CROSS-DOCUMENT hand-offs are vetoed. A same-document `navigate` — what
 * `history.pushState` and a hash change raise, and what a real router does on
 * every route change — leaves the iframe intact, so vetoing it would break
 * routing in exchange for nothing.
 */

import { vi } from "vitest";

/** A hand-off the guard vetoed: where the runner was asked to go. */
export interface NavigationEscape {
  /** Absolute URL of the destination, as the Navigation API reported it. */
  readonly url: string;
}

/**
 * The opening words of every escape failure. A consumer matches on this rather
 * than on the whole sentence, which also carries the URL.
 */
export const NAVIGATION_ESCAPE_MESSAGE = "Navigation escaped the test iframe";

/** Every escape vetoed since the last clear, in arrival order. */
const escapes: NavigationEscape[] = [];

/**
 * The `Navigation` the listener is attached to, so a second `install` on the same
 * page is a no-op while a genuinely new browser context still gets its own
 * listener. A setup file re-evaluates per context, and each context is a
 * different `navigation`.
 */
let guarded: Navigation | undefined;

/**
 * Record and veto. Registered by name rather than as a closure so a duplicate
 * `addEventListener` would be collapsed by the DOM even if `guarded` were ever
 * defeated.
 */
function vetoNavigation(event: NavigateEvent): void {
  if (event.destination.sameDocument) {
    return;
  }

  escapes.push({ url: event.destination.url });

  // A traversal the page is not allowed to stop reports `cancelable: false`;
  // recording it is still worth doing, since it explains the teardown that follows.
  if (event.cancelable) {
    event.preventDefault();
  }
}

/**
 * Register the `navigate` veto on the current page. Idempotent — a project's
 * setup file calls it once per browser context, and a second call must neither
 * double-record an escape nor leak a second listener.
 */
export function installNavigationEscapeGuard(): void {
  // Chromium ships the Navigation API and is the only browser this preset drives,
  // but a setup file must not throw where it is absent.
  if (!("navigation" in globalThis)) {
    return;
  }

  if (guarded === globalThis.navigation) {
    return;
  }

  guarded = globalThis.navigation;
  guarded.addEventListener("navigate", vetoNavigation);
}

/** Every escape vetoed since the last clear, in arrival order. */
export function navigationEscapes(): readonly NavigationEscape[] {
  return [...escapes];
}

/** Forget every recorded escape. */
export function clearNavigationEscapes(): void {
  escapes.length = 0;
}

/** Options shared by the consuming helpers. */
export interface ConsumeNavigationEscapeOptions {
  /** How long to wait for the hand-off to arrive, in ms. */
  readonly timeout?: number;
}

/**
 * The opening words of the failure raised when a spec asserted a hand-off that
 * never happened. Distinct from `NAVIGATION_ESCAPE_MESSAGE`, which is the
 * opposite defect.
 */
export const NO_NAVIGATION_ESCAPE_MESSAGE = "No navigation escaped the test iframe";

/**
 * Wait for the guard to veto a hand-off, then take the entries it holds at that
 * moment out of the shared record and return them.
 *
 * This is how a spec asserts a hand-off it MEANT to provoke. Registering a second
 * `navigate` listener beside the guard's does not work: both fire, the spec is
 * satisfied, and the project's `afterEach` still fails the test over an escape
 * nobody told it was expected. Consuming is also why this is not
 * `clearNavigationEscapes()` — a spec that forgets to call it leaves the escape
 * in the record, so the `afterEach` still fails, and nothing is suppressed by
 * being ignored.
 *
 * Only what was READ is removed, by count rather than by emptying the array, so a
 * second hand-off arriving while this awaited is still there for the `afterEach`.
 */
export async function consumeNavigationEscapes(
  options: ConsumeNavigationEscapeOptions = {},
): Promise<readonly NavigationEscape[]> {
  await vi.waitFor(
    () => {
      if (escapes.length === 0) {
        throw new Error(
          `${NO_NAVIGATION_ESCAPE_MESSAGE}. Nothing this test did handed the browser off, so there was nothing to assert — check that the action under test actually runs, and that it navigates cross-document rather than through the router.`,
        );
      }
    },
    options.timeout === undefined ? undefined : { timeout: options.timeout },
  );

  const consumed: NavigationEscape[] = [...escapes];
  escapes.splice(0, consumed.length);

  return consumed;
}

/**
 * Consume, and answer with the one hand-off — throwing, and naming each URL, when
 * the test provoked more than one. A screen finishing an OIDC flow hands off
 * exactly once; two means something navigated that the spec never accounted for.
 */
export async function expectNavigationEscape(
  options: ConsumeNavigationEscapeOptions = {},
): Promise<NavigationEscape> {
  const consumed: readonly NavigationEscape[] = await consumeNavigationEscapes(options);
  const [escape] = consumed;

  if (escape === undefined || consumed.length > 1) {
    const destinations: string = consumed.map((entry) => `  ${entry.url}`).join("\n");

    throw new Error(
      `Expected exactly one navigation, but ${consumed.length} were vetoed:\n${destinations}`,
    );
  }

  return escape;
}

/**
 * Throw — naming each escaped URL — when any hand-off was vetoed since the last
 * clear, then clear, so one escape fails one test rather than every test behind
 * it. This is what a project's `afterEach` calls.
 */
export function assertNoNavigationEscape(): void {
  if (escapes.length === 0) {
    return;
  }

  const destinations = escapes.map((escape) => `  ${escape.url}`).join("\n");
  clearNavigationEscapes();

  throw new Error(
    `${NAVIGATION_ESCAPE_MESSAGE}. The guard vetoed every destination below, so this test failed instead of the runner dying and blaming another file:\n${destinations}\nWhatever navigated has to prevent its own default — check that a router stub's onClick is destructured out of the props spread rather than declared beside it.`,
  );
}
