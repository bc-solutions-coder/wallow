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
 * Restyle spec for the register-app page (Wallow-urec.4.1) — the form-page half
 * of the WORKED EXAMPLE. This page is chrome only: `RegisterAppForm` already
 * renders through the shared `ui` primitives, so the restyle adds a heading and
 * a width and changes nothing inside the form. Its behaviour stays pinned by
 * `register.test.tsx` and `RegisterAppForm.test.tsx`, which the restyle must not
 * edit.
 *
 * Wallow-lrlm.5.1 retired the NARROW shell this form page used to opt into: the
 * width is now the one shared `PAGE_CONTAINER` every dashboard page takes.
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

    // Wallow-lrlm.5.1: `PageHeader`'s derived testid and `Text`'s title scale;
    // the page rhythm (`mb-8`) moved from the `<h1>` onto the header row.
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
