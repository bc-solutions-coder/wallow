/**
 * The unharnessed-request guard a browser project installs once, in its setup
 * file.
 *
 * `createSdkHarness()` injects its transport into `createWallowSdk` and never
 * touches the global, which is what keeps two concurrent specs from seeing each
 * other's calls. The corollary is that anything arriving at `globalThis.fetch`
 * is traffic NO harness owns — a screen reaching past the router-context SDK, an
 * operation the spec forgot to program, a component fetching on mount.
 *
 * Unguarded, that request leaves for the real network. In CI there is nothing
 * behind the URL, so it fails as a hang and gets blamed on the assertion that
 * happened to be waiting; on a developer's machine with `pnpm backend` running
 * it SUCCEEDS, and the spec passes against a live database. Both readings are
 * wrong in a way the report cannot show.
 *
 * So an escape is answered rather than forwarded: a 503 whose body names the
 * request. Answering matters as much as recording — a request left to hang turns
 * the failure back into a timeout on some later line.
 */

import { vi } from "vitest";

/** One request the guard answered instead of letting it leave. */
export interface NetworkEscape {
  /** Uppercase HTTP method, e.g. `POST`. */
  readonly method: string;
  /** Absolute URL the request was issued against. */
  readonly url: string;
}

/**
 * The opening words of every escape failure. A consumer matches on this rather
 * than on the whole sentence, which also carries each method and URL.
 */
export const NETWORK_ESCAPE_MESSAGE = "A request escaped to the real network";

/**
 * The opening words of the failure raised when a spec asserted an escape that
 * never happened. Distinct from {@link NETWORK_ESCAPE_MESSAGE}, which is the
 * opposite defect.
 */
export const NO_NETWORK_ESCAPE_MESSAGE = "No request escaped to the real network";

/**
 * Same-origin path prefixes the Vite/Vitest machinery owns. These pass through
 * to the real transport: the runner fetches its own modules and talks to its own
 * API over the same `fetch` the guard wraps, so blocking them takes the runner
 * down rather than failing a test.
 */
const PASSTHROUGH_PREFIXES: readonly string[] = ["/__vitest", "/@"];

/** Answered with, rather than forwarded — "the harness has no route for this". */
const BLOCKED_STATUS = 503;

/** Every request answered since the last clear, in arrival order. */
const escapes: NetworkEscape[] = [];

/**
 * The real `fetch`, held while the guard is installed — and the idempotence
 * latch, so a second install neither double-records nor wraps its own wrapper.
 */
let originalFetch: typeof globalThis.fetch | undefined;

function isPassthrough(url: URL): boolean {
  return (
    url.origin === globalThis.location.origin &&
    PASSTHROUGH_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))
  );
}

/**
 * Wrap `globalThis.fetch` on the current page. Idempotent — a project's setup
 * file calls it once per browser context.
 */
export function installNetworkEscapeGuard(): void {
  if (originalFetch !== undefined) {
    return;
  }

  const real: typeof globalThis.fetch = globalThis.fetch.bind(globalThis);
  originalFetch = real;

  globalThis.fetch = (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    // Built rather than read off `input`, which may be a bare string or a URL —
    // `Request` is what normalises both, and resolves a relative path against
    // the page origin so the passthrough test has an origin to compare.
    const request: Request = new Request(input, init);
    const url: URL = new URL(request.url);

    if (isPassthrough(url)) {
      return real(input, init);
    }

    escapes.push({ method: request.method, url: request.url });

    return Promise.resolve(
      Response.json(
        { title: `${NETWORK_ESCAPE_MESSAGE}: ${request.method} ${request.url}` },
        { status: BLOCKED_STATUS },
      ),
    );
  };
}

/**
 * Put the real `fetch` back and release the idempotence latch.
 *
 * Only this module's own spec has business calling it: a project installs the
 * guard for the whole run, and a spec that means to provoke an escape consumes
 * the record instead.
 */
export function uninstallNetworkEscapeGuard(): void {
  if (originalFetch === undefined) {
    return;
  }

  globalThis.fetch = originalFetch;
  originalFetch = undefined;
}

/** Every request answered since the last clear, in arrival order. */
export function networkEscapes(): readonly NetworkEscape[] {
  return [...escapes];
}

/** Forget every recorded request. */
export function clearNetworkEscapes(): void {
  escapes.length = 0;
}

/** Options shared by the consuming helpers. */
export interface ConsumeNetworkEscapeOptions {
  /** How long to wait for the first request to arrive, in ms. */
  readonly timeout?: number;
}

/**
 * Wait for at least one escape, then take everything the record holds at that
 * moment out of it and return it.
 *
 * Consuming rather than clearing is what keeps "a spec that forgot to assert
 * still fails" true: an entry nobody reads is still there for the project's
 * `afterEach`. Only what was READ is removed, by count rather than by emptying
 * the array, so a request issued while this awaited survives to fail the test.
 */
export async function consumeNetworkEscapes(
  options: ConsumeNetworkEscapeOptions = {},
): Promise<readonly NetworkEscape[]> {
  await vi.waitFor(
    () => {
      if (escapes.length === 0) {
        throw new Error(
          `${NO_NETWORK_ESCAPE_MESSAGE}. Nothing this test did reached the global fetch, so there was nothing to assert — check that the action under test actually runs, and that it goes out over the global rather than through the harness transport.`,
        );
      }
    },
    options.timeout === undefined ? undefined : { timeout: options.timeout },
  );

  const consumed: NetworkEscape[] = [...escapes];
  escapes.splice(0, consumed.length);

  return consumed;
}

/**
 * Throw — naming each request — when anything reached the global `fetch` since
 * the last clear, then clear, so one escape fails one test rather than every
 * test behind it. This is what a project's `afterEach` calls.
 */
export function assertNoNetworkEscape(): void {
  if (escapes.length === 0) {
    return;
  }

  const requests: string = escapes.map((escape) => `  ${escape.method} ${escape.url}`).join("\n");
  clearNetworkEscapes();

  throw new Error(
    `${NETWORK_ESCAPE_MESSAGE}. Every request below was answered with a ${BLOCKED_STATUS} rather than sent, so this test failed instead of hanging in CI or passing against a live backend. Program the harness for the operation, or — for a spec that provoked the request deliberately — consume it with consumeNetworkEscapes():\n${requests}`,
  );
}
