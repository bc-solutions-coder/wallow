import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock } from "../../../test/sdk-client-mock";
import {
  byTestId,
  expectClasses,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
} from "../../../test/style-contract";
import { Route } from "./index";

/**
 * Restyle spec for the apps index page (Wallow-urec.4.1) — the WORKED EXAMPLE
 * for Phase 4. It asserts only the page chrome the restyle adds; the route's
 * behaviour (loader, `dashboard-apps` root, `apps-register-link` href) stays
 * pinned by the sibling `index.test.tsx`, which the restyle must not edit.
 *
 * The page renders with a seeded, non-empty `['apps']` cache so the whole page —
 * header row plus populated list — is on screen for the token-color scan.
 */

function newClient(): QueryClient {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
  client.setQueryData(
    ["apps"],
    [
      {
        clientId: "c1",
        displayName: "Acme App",
        clientType: "public",
        redirectUris: [],
        createdAt: null,
      },
    ],
  );
  return client;
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithClient(<Page />);
  return waitForTestId("dashboard-apps");
}

describe("routes/dashboard/apps (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("centers the page body in the dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto");
  });

  it("titles the page with an h1 reading My Apps", async () => {
    await renderPage();

    const heading = byTestId("apps-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("My Apps");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("lays the heading and the register CTA out in one header row", async () => {
    await renderPage();

    const headerRow = parentOf(byTestId("apps-heading"));
    expectClasses(headerRow, "flex items-center justify-between mb-8");
    expect(headerRow.contains(byTestId("apps-register-link"))).toBe(true);
  });

  it("styles the register link as the gold pill CTA", async () => {
    await renderPage();

    const link = byTestId("apps-register-link");
    expectClasses(
      link,
      "bg-primary text-primary-foreground font-medium px-6 py-2.5 rounded-full hover:opacity-90 no-underline text-sm transition-colors",
    );
    // Regression guard: the pill is still the same link, with the same words.
    expect(link.getAttribute("href")).toBe("/dashboard/apps/register");
    expect(link.textContent?.trim()).toBe("Register New App");
  });

  it("renders the header row above the app list", async () => {
    await renderPage();

    expectPrecedes(byTestId("apps-heading"), byTestId("apps-table"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
