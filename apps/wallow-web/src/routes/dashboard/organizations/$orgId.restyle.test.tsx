import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, it, vi } from "vitest";

import { installSdkClientMock } from "../../../test/sdk-client-mock";
import { expectClasses, expectTokenColorsOnly, waitForTestId } from "../../../test/style-contract";

/**
 * Restyle spec for the organization-detail page shell (Wallow-urec.4.3). Only
 * the route's own chrome is asserted here — the shell width — because the page
 * body is `OrganizationDetail`, which has its own restyle spec. The route's
 * structural contract (component + loader + router registration) stays pinned by
 * the sibling `$orgId.test.tsx`, which the restyle must not edit.
 *
 * The page reads `Route.useParams()`, which throws outside a `RouterProvider`,
 * so `createFileRoute` is stubbed to hand back a plain route object with a fixed
 * `orgId` — the same "mock the router module to render a route standalone"
 * approach `__root.focus.test.tsx` uses for `Outlet`/`FocusOnNavigate`.
 *
 * Blazor reference (`2e039fcb:...Dashboard/OrganizationDetail.razor`): the detail
 * page uses the WIDE `max-w-5xl` shell, not the narrow form shell — it carries
 * two tables.
 */
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@tanstack/react-router")>();
  return {
    ...actual,
    createFileRoute: () => (options: Record<string, unknown>) => ({
      options,
      useParams: () => ({ orgId: "o1" }),
    }),
  };
});

const ORG = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

const MEMBERS = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["Owner"],
  },
];

const CLIENTS = [{ id: "c1", clientId: "acme-web", name: "Acme Web" }];

function newClient(): QueryClient {
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });
  client.setQueryData(["orgs", "o1"], ORG);
  client.setQueryData(["orgs", "o1", "members"], MEMBERS);
  client.setQueryData(["orgs", "o1", "clients"], CLIENTS);
  return client;
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const { Route } = await import("./$orgId");
  const Page = Route.options.component!;
  renderWithClient(<Page />);
  return waitForTestId("dashboard-organization-detail");
}

describe("routes/dashboard/organizations/$orgId (restyle)", () => {
  beforeEach(() => {
    const sdk = installSdkClientMock();
    // Any unseeded query (a refetch after a lifecycle mutation) resolves empty
    // so no list render ever sees a non-array body.
    sdk.resolveJson([]);
  });

  it("centers the detail page in the wide dashboard shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-5xl mx-auto");
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
