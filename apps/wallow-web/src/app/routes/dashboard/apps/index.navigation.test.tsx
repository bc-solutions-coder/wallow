import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { AnyRouter } from "@tanstack/react-router";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { byTestId, expectClasses, expectTag, waitForTestId } from "@shared/testing/style-contract";
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
  const { router } = renderWithWallow(<Page />, { harness });
  await waitForTestId("apps-register-link");
  return router;
}

/** None of `classes` is on `element` — the absence half of the style contract. */
function expectNoClasses(element: Element, classes: string): void {
  const present: string[] = classes
    .split(/\s+/u)
    .filter((cls) => cls !== "" && element.classList.contains(cls));
  expect(present, `unexpected classes on <${element.tagName.toLowerCase()}>`).toEqual([]);
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

  it("keeps the gold pill look", async () => {
    await renderPage();

    expectClasses(
      byTestId("apps-register-link"),
      "bg-primary text-primary-foreground font-medium rounded-full px-6 py-2.5 text-sm no-underline",
    );
  });

  it("does not stretch the CTA across the header row", async () => {
    await renderPage();

    // `buttonRecipe`'s width defaults to `full` and its shape to `rounded`;
    // both must be overridden for the pill to keep its shipped footprint.
    expectNoClasses(byTestId("apps-register-link"), "w-full rounded-md");
  });

  it("carries the catalog Button's hover, focus-visible and motion treatment", async () => {
    await renderPage();

    expectClasses(
      byTestId("apps-register-link"),
      "inline-flex items-center justify-center outline-none motion-safe:transition-colors focus-visible:ring-2 focus-visible:ring-ring hover:bg-primary/90 data-[disabled]:opacity-50",
    );
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
