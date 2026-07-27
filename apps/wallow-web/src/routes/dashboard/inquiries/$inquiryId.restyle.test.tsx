import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, it, vi } from "vitest";

import { installSdkClientMock } from "../../../test/sdk-client-mock";
import { expectClasses, expectTokenColorsOnly, waitForTestId } from "../../../test/style-contract";
import { Route } from "./$inquiryId";

/**
 * Restyle spec for the inquiry-detail route shell (Wallow-urec.4.2). The route's
 * structural contract (component, loader, router registration) stays pinned by
 * the sibling `$inquiryId.test.tsx`, which the restyle must not edit, and the
 * page body's chrome belongs to `InquiryDetail.restyle.test.tsx` — all this file
 * owns is the column width the detail page sits in.
 *
 * The page reads `Route.useParams()`, which needs a mounted router; rather than
 * stand a whole `RouterProvider` up for two class assertions, the param hook is
 * stubbed on the route object itself (`vi.spyOn`), the same "render the page
 * standalone" trick the other route specs get for free by not reading params.
 */

const INQUIRY = {
  id: "i1",
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

function newClient(): QueryClient {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
  client.setQueryData(["inquiries", "i1"], INQUIRY);
  client.setQueryData(["inquiries", "i1", "comments"], []);
  return client;
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithClient(<Page />);
  return waitForTestId("dashboard-inquiry-detail");
}

describe("routes/dashboard/inquiries/$inquiryId (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
    vi.spyOn(Route, "useParams").mockReturnValue({ inquiryId: "i1" });
  });

  it("constrains the detail page to the narrow column", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-2xl mx-auto");
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
