import { describe, expect, it } from "vitest";

import { forkBranding, forkResolvedBranding, renderThemeStyle } from "./lib/branding";
import { render } from "./ssr";

/**
 * The unmatched-URL contract for wallow-auth (Wallow-ffpq.2.7).
 *
 * Driven through the SSR entry (`src/ssr.tsx`) rather than by mounting a
 * component, because both halves of this bead's acceptance criteria are
 * properties of the RESPONSE, not of a component: the HTTP status and the markup
 * the router renders when nothing matched. `server.ts`'s host propagates the SSR
 * entry's own status verbatim ("the router owns every remaining path, 404s
 * included" — `@bc-solutions-coder/web-shell`'s `standalone-host.ts`, pinned by
 * its own "propagates the SSR entry's own status (e.g. a 404 route)" spec), so
 * the status asserted here is the status `curl -I` sees against the built host.
 *
 * WHICH SIDE IS ALREADY GREEN: the status and the shell. An unmatched path
 * already answers 404 (live-verified against the built host during this bead's
 * audit) and already renders through the document shell, so "answers 404, not
 * 200", "arrives as a themed document", and the `/login` guard were green the
 * moment they were written — they pin behaviour this bead must not regress and
 * are deliberately not part of its RED count. What is red is the page itself:
 * no not-found component exists anywhere in the app today, so an unmatched auth
 * URL answers 404 with the framework's bare "Not Found" inside an otherwise
 * branded document.
 *
 * The wiring site is left to the implementation: a root-route
 * `notFoundComponent` and a router-level `defaultNotFoundComponent` are both
 * correct, and nothing here reads either option, so neither choice is forced.
 */

/**
 * A path no route claims, carrying a marker string that must never reach the
 * screen. `/<anything>` is a URL an attacker can construct and send to a victim,
 * so the marker doubles as the echo probe — a not-found screen that prints the
 * path it was asked for would put attacker-chosen text on a Wallow-branded auth
 * page.
 */
const ECHO_MARKER = "call-555-1234-for-your-refund";
const UNMATCHED_PATH = `/no-such-auth-page/${ECHO_MARKER}`;

const NOT_FOUND = 404;
const OK = 200;

interface Rendered {
  readonly status: number;
  readonly html: string;
  readonly document: Document;
}

async function renderPath(path: string): Promise<Rendered> {
  const response: Response = await render(new Request(`http://localhost:3002${path}`));
  const html: string = await response.text();

  return {
    status: response.status,
    html,
    document: new DOMParser().parseFromString(html, "text/html"),
  };
}

describe("an auth URL no route claims", () => {
  it("answers 404, not 200", async () => {
    // CHARACTERIZATION (already green). A 200 with a "page not found" body is
    // the failure mode that matters beyond cosmetics: crawlers index it, and
    // monitoring cannot tell a mistyped auth link from a working one.
    const { status } = await renderPath(UNMATCHED_PATH);

    expect(status).toBe(NOT_FOUND);
  });

  it("renders the not-found screen rather than the framework's bare fallback", async () => {
    const { document } = await renderPath(UNMATCHED_PATH);

    const heading: Element | null = document.querySelector('[data-testid="not-found-heading"]');

    expect(heading).not.toBeNull();
    expect(heading?.textContent).toMatch(/not found/iu);
  });

  it("explains the miss in its own line of copy", async () => {
    const { document } = await renderPath(UNMATCHED_PATH);

    const message: Element | null = document.querySelector('[data-testid="not-found-message"]');

    expect(message).not.toBeNull();
    expect((message?.textContent ?? "").trim().length).toBeGreaterThan(0);
  });

  it("offers a link back to /login", async () => {
    // The one action this page can usefully offer in an auth app.
    const { document } = await renderPath(UNMATCHED_PATH);

    const link: Element | null = document.querySelector('[data-testid="not-found-login-link"]');

    expect(link?.getAttribute("href")).toBe("/login");
  });

  it("renders inside the branded auth chrome, like every other auth screen", async () => {
    // A 404 is still a page of this app: it must arrive with the fork's logo,
    // name, and fork attribution — i.e. wrapped in `AuthLayout` — rather than as
    // unstyled text on a themed document. The `<h1>` carrying
    // `data-focus-target` is `AuthLayout`'s own header, so its presence is the
    // proof the layout wrapped this screen.
    const { document } = await renderPath(UNMATCHED_PATH);

    const heading: Element | null = document.querySelector("h1[data-focus-target]");

    expect(heading?.textContent).toBe(forkBranding.appName);
  });

  it("arrives as a themed document, not a bare host error", async () => {
    // The shell still runs: the fork's <title> and the theme custom properties
    // the chrome's classes resolve against are in the head of the 404 exactly as
    // on /login. This is what separates a router-rendered 404 from the host's own
    // fallthrough response.
    const { document } = await renderPath(UNMATCHED_PATH);

    expect(document.querySelector("title")?.textContent).toBe(forkBranding.appName);
    expect(document.querySelector("head style")?.textContent).toContain(
      renderThemeStyle(forkResolvedBranding),
    );
  });

  it("never echoes the requested path onto the page", async () => {
    // Scoped to the not-found screen's own elements rather than the whole
    // document: TanStack dehydrates the router state (the requested href
    // included) into an inline script, which is not screen copy and is not what
    // this asserts.
    const { document } = await renderPath(UNMATCHED_PATH);

    const rendered: Element[] = [...document.querySelectorAll('[data-testid^="not-found"]')];

    expect(rendered.length).toBeGreaterThan(0);
    for (const element of rendered) {
      expect(element.textContent ?? "").not.toContain(ECHO_MARKER);
    }
  });
});

describe("the auth URLs that DO exist", () => {
  it("still serves /login itself, with no trace of the not-found screen", async () => {
    // The guard on the wiring: a `defaultNotFoundComponent` (or root
    // `notFoundComponent`) misapplied — e.g. registered as the root route's
    // `component`, or tripped by a loader that throws `notFound()` — would turn
    // every screen into this page while still returning 200. Pinning a real
    // route here is what makes the red tests above safe to make green.
    const { status, document } = await renderPath("/login");

    expect(status).toBe(OK);
    expect(document.querySelector('[data-testid="not-found-heading"]')).toBeNull();
    expect(document.querySelector("h1[data-focus-target]")?.textContent).toBe(forkBranding.appName);
  });
});
