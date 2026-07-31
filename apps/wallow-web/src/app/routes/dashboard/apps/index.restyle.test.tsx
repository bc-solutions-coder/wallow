import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectClasses,
  expectPageContainer,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "@shared/testing/style-contract";
import { Route } from "./index";

/**
 * Page chrome for the apps index: the shared container, the header row, and the
 * register CTA's styling.
 *
 * Renders with a non-empty list so header row and populated list are both on
 * screen for the token-colour scan.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Render the route page and resolve its settled root element.
 *
 * Gated on the list as well as the root: the root paints immediately, but the
 * list only replaces its loading state once the harness answers, and reading
 * `apps-table` synchronously after the root would race that response.
 */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  const root = await waitForTestId("dashboard-apps");
  await waitForTestId("apps-table");
  return root;
}

describe("routes/dashboard/apps (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      "GET /v1/identity/apps": [
        {
          clientId: "c1",
          displayName: "Acme App",
          clientType: "public",
          redirectUris: [],
          createdAt: null,
        },
      ],
    });
  });

  it("centers the page body in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("titles the page with an h1 reading My Apps", async () => {
    await renderPage();

    // The heading is `PageHeader`'s own `<h1>`, so its testid is DERIVED from the
    // header's: `apps-header` -> `apps-header-title`.
    const heading = byTestId("apps-header-title");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("My Apps");
    expectClasses(heading, "text-3xl font-bold tracking-tight text-foreground");
  });

  it("lays the heading and the register CTA out in one header row", async () => {
    await renderPage();

    const header = byTestId("apps-header");
    expectClasses(header, "flex items-start justify-between gap-4 mb-8");
    expect(header.contains(byTestId("apps-header-title"))).toBe(true);
    expect(header.contains(byTestId("apps-register-link"))).toBe(true);
  });

  it("puts the register CTA in the header's trailing actions slot", async () => {
    await renderPage();

    const actions = byTestId("apps-header-actions");
    expectClasses(actions, "flex items-center gap-3 shrink-0");
    expect(actions.contains(byTestId("apps-register-link"))).toBe(true);
    expectPrecedes(byTestId("apps-header-title"), actions);
  });

  it("styles the register link as the gold pill CTA", async () => {
    await renderPage();

    const link = byTestId("apps-register-link");
    expectClasses(
      link,
      "bg-primary text-primary-foreground font-medium px-6 py-2.5 rounded-full no-underline text-sm hover:bg-primary/90 motion-safe:transition-colors focus-visible:ring-2 focus-visible:ring-ring",
    );
    expect(link.getAttribute("href")).toBe("/dashboard/apps/register");
    expect(link.textContent?.trim()).toBe("Register New App");
  });

  it("renders the header row above the app list", async () => {
    await renderPage();

    expectPrecedes(byTestId("apps-header-title"), byTestId("apps-table"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
