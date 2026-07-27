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
import { Route } from "./register";

/**
 * Restyle spec for the register-app page (Wallow-urec.4.1) — the form-page half
 * of the WORKED EXAMPLE. Form pages get the NARROW shell (`max-w-2xl`) and page
 * chrome only: `RegisterAppForm` already renders through the shared `ui`
 * primitives, so the restyle adds a heading and a width and changes nothing
 * inside the form. Its behaviour stays pinned by `register.test.tsx` and
 * `RegisterAppForm.test.tsx`, which the restyle must not edit.
 */

function renderWithClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** Render the route page and resolve its settled root element. */
async function renderPage(): Promise<HTMLElement> {
  const Page = Route.options.component!;
  renderWithClient(<Page />);
  return waitForTestId("dashboard-apps-register");
}

describe("routes/dashboard/apps/register (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("constrains the form page to the narrow shell", async () => {
    const root = await renderPage();

    expectClasses(root, "max-w-2xl mx-auto");
  });

  it("titles the page with an h1 reading Register New App", async () => {
    await renderPage();

    const heading = byTestId("apps-register-heading");
    expectTag(heading, "h1");
    expect(heading.textContent).toBe("Register New App");
    expectClasses(heading, "text-3xl font-bold text-foreground mb-8");
  });

  it("keeps the register form mounted below the heading", async () => {
    await renderPage();

    expectPrecedes(byTestId("apps-register-heading"), byTestId("app-register-form"));
  });

  it("styles the page with theme tokens only", async () => {
    const root = await renderPage();

    expectTokenColorsOnly(root);
  });
});
