import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
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
import { Route } from "./register";

/**
 * Page chrome for the register-app page: the shared container and the page
 * heading. This page is chrome only — `RegisterAppForm` renders through the
 * shared `ui` primitives and owns everything inside the form.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithWallow(<Page />, { harness });
  return waitForTestId("dashboard-apps-register");
}

describe("routes/dashboard/apps/register (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("centers the form page in the shared dashboard container", async () => {
    const root = await renderPage();

    expectPageContainer(root);
  });

  it("titles the page with an h1 reading Register New App", async () => {
    await renderPage();

    // `PageHeader`'s derived testid; the page rhythm (`mb-8`) sits on the header
    // ROW, not on the `<h1>`.
    const heading = byTestId("apps-register-header-title");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Register New App");
    expectClasses(heading, "text-3xl font-bold tracking-tight text-foreground");
    expectClasses(byTestId("apps-register-header"), "flex items-start justify-between gap-4 mb-8");
  });

  it("keeps the register form mounted below the heading", async () => {
    await renderPage();

    expectPrecedes(byTestId("apps-register-header-title"), byTestId("app-register-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
