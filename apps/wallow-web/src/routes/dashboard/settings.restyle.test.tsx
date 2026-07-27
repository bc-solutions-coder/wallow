import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock } from "../../test/sdk-client-mock";
import {
  byTestId,
  expectClasses,
  expectPrecedes,
  expectTag,
  expectTokenColorsOnly,
  waitForTestId,
} from "../../test/style-contract";
import { Route } from "./settings";

/**
 * Restyle spec for the settings page (Wallow-urec.4.4), following the worked
 * example in `routes/dashboard/apps/index.restyle.test.tsx`. Settings is
 * form-heavy rather than a table, so it takes the NARROW shell (`max-w-2xl`) and
 * leans on the `ui` Card sections rather than the list-card surface.
 *
 * Only page chrome is asserted here; the route's behaviour (loader, the
 * `dashboard-settings` root, and the fact that both sections mount inside it)
 * stays pinned by the sibling `settings.test.tsx`, which the restyle must not
 * edit. The page renders with both caches seeded so the profile and MFA sections
 * are on screen (not in their loading states) for the token-color scan.
 */

const PROFILE = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner"],
  permissions: [],
};

const MFA_STATUS = { enabled: false, method: null, backupCodeCount: 0 };

function newClient(): QueryClient {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } },
  });
  client.setQueryData(["settings", "profile"], PROFILE);
  client.setQueryData(["mfa", "status"], MFA_STATUS);
  return client;
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithClient(<Page />);
  return waitForTestId("dashboard-settings");
}

describe("routes/dashboard/settings (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("constrains the settings page to the narrow shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-2xl mx-auto");
  });

  it("titles the page with an h1 reading Settings", async () => {
    await renderPage();

    const heading = byTestId("settings-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Settings");
    expectClasses(heading, "text-3xl font-bold text-foreground mb-8");
  });

  it("renders the heading above the profile section and the MFA section", async () => {
    await renderPage();

    expectPrecedes(byTestId("settings-heading"), byTestId("settings-profile-name"));
    expectPrecedes(byTestId("settings-profile-name"), byTestId("settings-mfa-status"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
