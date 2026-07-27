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
  waitForTestId,
} from "../../../test/style-contract";
import { Route } from "./index";

/**
 * Restyle spec for the inquiries index page (Wallow-urec.4.2), following the
 * `.4.1` apps worked example. It asserts only the page chrome the restyle adds;
 * the route's behaviour (loader, `dashboard-inquiries` root, the inline
 * `inquiry-create-form`) stays pinned by the sibling `index.test.tsx`, which the
 * restyle must not edit.
 *
 * This page is the one list page in Phase 4 with NO call-to-action link, so it
 * takes `register.tsx`'s heading treatment (a bare `h1`) rather than `.4.1`'s
 * `flex items-center justify-between` header row — a flex row holding a single
 * child would be chrome with nothing to align. Vertical rhythm between the
 * heading, the list, and the create card comes from `space-y-8` on the shell.
 */

function newClient(): QueryClient {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
  client.setQueryData(
    ["inquiries"],
    [
      {
        id: "i1",
        name: "Ada Lovelace",
        email: "ada@example.com",
        company: null,
        projectType: "web-app",
        status: "New",
        createdAt: "2026-07-15T00:00:00Z",
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
  return waitForTestId("dashboard-inquiries");
}

describe("routes/dashboard/inquiries (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("centers the page body in the dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto space-y-8");
  });

  it("titles the page with an h1 reading Inquiries", async () => {
    await renderPage();

    const heading = byTestId("inquiries-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Inquiries");
    expectClasses(heading, "text-3xl font-bold text-foreground");
  });

  it("renders the heading above the inquiry list", async () => {
    await renderPage();

    expectPrecedes(byTestId("inquiries-heading"), byTestId("inquiries-table"));
  });

  it("keeps the create form below the list", async () => {
    await renderPage();

    // Regression guard: the restyle reorders nothing — list first, create second.
    expectPrecedes(byTestId("inquiries-table"), byTestId("inquiry-create-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
