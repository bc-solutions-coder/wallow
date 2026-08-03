import {
  computedColor,
  effectiveBackground,
  isTransparent,
  textContrast,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { AnyRouter } from "@tanstack/react-router";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { byTestId, expectTag, waitForTestId } from "@bc-solutions-coder/testing/locators";
import { Route } from "./index";

/**
 * The apps index "Register New App" CTA: a real anchor whose click the ROUTER
 * takes, asserted against the memory router's own location — that is the
 * difference between a client-side navigation and a document load.
 *
 * The element must stay announced as a LINK: Base UI's `Button` merges
 * `role="button"` onto whatever it substitutes when `nativeButton={false}`, and
 * stamps `type` when `nativeButton` is left at its default. `buttonRecipe`'s
 * width defaults to `full`, so the pill needs `width="auto"` to keep its footprint.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Render the apps index page and hand back the router driving it.
 *
 * The list resolves empty — an empty list still paints the whole header row.
 * Gated on the CTA itself so every assertion below reads a settled DOM.
 */
async function renderPage(): Promise<AnyRouter> {
  harness.resolveJson([]);
  const Page = Route.options.component!;
  const { router } = renderWithWallow(
    <>
      <div data-testid="probe-primary" className="bg-primary size-4" />
      <Page />
    </>,
    { harness },
  );
  await waitForTestId("apps-register-link");
  return router;
}

/** WCAG 2.1 AA for body-sized text; the CTA's label is `text-sm`. */
const AA_TEXT = 4.5;

/** Bounds the colour polls below. Tailwind's own duration is 150ms. */
const TRANSITION_TIMEOUT = 2000;

/** The reference colour `bg-primary` paints, failing loudly on a theme-less page. */
function primarySurface(): Rgba {
  const primary: Rgba = computedColor(byTestId("probe-primary"), "background-color");
  expect(isTransparent(primary), "bg-primary paints nothing — is the fork theme loaded?").toBe(
    false,
  );
  return primary;
}

/**
 * Click `element` and report whether the click was CLAIMED — whether something
 * had already called `preventDefault()` by the time it reached the document.
 *
 * The listener is what makes this spec survivable. A raw anchor's click is a
 * real document load, and in browser mode that navigates the test iframe away
 * and kills the whole FILE ("Cannot connect to the iframe"). The default is
 * swallowed at the document in the BUBBLE phase — after React's root handler,
 * so a TanStack `Link` still gets its chance to claim the click. It costs the
 * spec nothing: the router assertion reads the router's own location, which a
 * document load never reaches.
 */
async function clickAndReportClaimed(element: HTMLElement): Promise<boolean> {
  let claimed = false;
  const swallow = (event: Event): void => {
    claimed = event.defaultPrevented;
    event.preventDefault();
  };

  document.addEventListener("click", swallow);
  try {
    await userEvent.click(element);
  } finally {
    document.removeEventListener("click", swallow);
  }

  return claimed;
}

describe("routes/dashboard/apps register CTA navigation", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the register CTA as an anchor rather than a button", async () => {
    await renderPage();

    expectTag(byTestId("apps-register-link"), "a");
  });

  it("keeps the CTA addressable at the register route", async () => {
    await renderPage();

    expect(byTestId("apps-register-link").getAttribute("href")).toBe("/dashboard/apps/register");
  });

  it("keeps the CTA's words", async () => {
    await renderPage();

    expect(byTestId("apps-register-link").textContent?.trim()).toBe("Register New App");
  });

  it("navigates through the router when the CTA is clicked", async () => {
    const router = await renderPage();

    await clickAndReportClaimed(byTestId("apps-register-link"));

    await expect.poll(() => router.state.location.pathname).toBe("/dashboard/apps/register");
  });

  it("lets the router claim the click instead of the browser", async () => {
    await renderPage();

    // Read at the event rather than at the router: a raw anchor lets its click
    // reach the document untouched, and the browser performs a document load.
    expect(await clickAndReportClaimed(byTestId("apps-register-link"))).toBe(true);
  });

  it("leaves the CTA announced as a link, not a button", async () => {
    await renderPage();
    const cta = byTestId("apps-register-link");

    // Read through the ROLE ENGINE, not the `role` attribute: the catalog Button
    // may announce a mounted anchor as a link either by setting `role="link"` or
    // by leaving the anchor's implicit role alone. Both are correct;
    // `role="button"` — what Base UI stamps on a substituted element — is not.
    expect(page.getByRole("link", { name: "Register New App" }).query()).toBe(cta);
    expect(page.getByRole("button", { name: "Register New App" }).query()).toBeNull();
    // `type` is what Base UI adds instead when `nativeButton` is left at its default.
    expect(cta.getAttribute("type")).toBeNull();
  });

  it("fills the CTA with the primary token and keeps its label legible on it", async () => {
    await renderPage();
    const cta = byTestId("apps-register-link");
    // The RESTING fill is the claim, and `buttonRecipe` gives the CTA a
    // `hover:bg-primary/90` arm. The pointer persists across cases and across
    // spec FILES, so an unnamed rest read can be a hover read.
    await userEvent.unhover(cta);

    // Measured against a probe, not read off `classList`: `cn()` merges a
    // caller's `className` over the recipe, so `bg-primary` can be present while
    // the anchor paints something else — and the probe is what makes "gold" mean
    // the primary token rather than any colour that happens not to be blank.
    // Polled, because a case that clicked the CTA leaves the pointer on it and
    // `motion-safe:transition-colors` takes 150ms to hand the resting fill back.
    await expect
      .poll(() => JSON.stringify(effectiveBackground(cta)), { timeout: TRANSITION_TIMEOUT })
      .toBe(JSON.stringify(primarySurface()));

    const ratio: number = textContrast(cta);

    expect(ratio, `the CTA's label measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(AA_TEXT);
  });

  it("renders the CTA as an undecorated pill", async () => {
    await renderPage();
    const cta = byTestId("apps-register-link");
    const styles: CSSStyleDeclaration = getComputedStyle(cta);

    // A pill's radius saturates at half its height; `rounded-md` measures ~6px.
    expect(Number.parseFloat(styles.borderTopLeftRadius)).toBeGreaterThanOrEqual(
      cta.getBoundingClientRect().height / 2,
    );
    // The CTA is a real anchor, so the underline is the browser's default.
    expect(styles.textDecorationLine).toBe("none");
  });

  it("does not stretch the CTA across the header row", async () => {
    await renderPage();

    // `buttonRecipe`'s width defaults to `full`, so the pill needs `width="auto"`
    // to keep its shipped footprint.
    expect(byTestId("apps-register-link").getBoundingClientRect().width).toBeLessThan(
      byTestId("apps-header").getBoundingClientRect().width,
    );
  });

  it("centres the CTA's label in a flex row", async () => {
    await renderPage();
    const styles: CSSStyleDeclaration = getComputedStyle(byTestId("apps-register-link"));

    // `inline-flex` computes to `flex` here: the CTA is a flex item of the
    // header's actions row, and a flex item's display is blockified.
    expect(styles.display).toBe("flex");
    expect(styles.alignItems).toBe("center");
    expect(styles.justifyContent).toBe("center");
  });

  it("repaints the CTA under the cursor", async () => {
    await renderPage();
    const cta = byTestId("apps-register-link");

    // Settle on the resting fill before reading it: a case that clicked the CTA
    // leaves the pointer on it, and a fill caught mid-transition makes
    // "repainted under the cursor" true of the transition rather than the hover.
    await userEvent.unhover(cta);
    await expect
      .poll(() => JSON.stringify(effectiveBackground(cta)), { timeout: TRANSITION_TIMEOUT })
      .toBe(JSON.stringify(primarySurface()));

    const resting: string = JSON.stringify(effectiveBackground(cta));

    await userEvent.hover(cta);

    // `buttonRecipe`'s base carries `motion-safe:transition-colors`, so a colour
    // read the moment the cursor lands is the resting colour caught mid-transition
    // — indistinguishable from "the hover fill never applied".
    await expect
      .poll(() => JSON.stringify(effectiveBackground(cta)), { timeout: TRANSITION_TIMEOUT })
      .not.toBe(resting);
  });

  it("reaches the CTA by keyboard", async () => {
    await renderPage();
    const cta = byTestId("apps-register-link");

    cta.focus();

    expect(document.activeElement).toBe(cta);
  });

  it("keeps the CTA inside the page header row", async () => {
    await renderPage();

    // The row is `PageHeader`'s root, addressed by its own testid rather than by
    // walking up from the heading — the heading's parent is the title/description
    // column, which the CTA is NOT in.
    const headerRow = byTestId("apps-header");
    expect(headerRow.contains(byTestId("apps-register-link"))).toBe(true);
  });
});
