import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock } from "../../../test/sdk-client-mock";
import { RegisterAppForm } from "./RegisterAppForm";

/**
 * App branding/logo-upsert reachability spec (Wallow-ffpq.3.6) — the optional
 * "Branding" section that upserts a display name, tagline, and logo file for the
 * app.
 * The branding section lives on the same register-app page, so once
 * `RegisterAppForm` is mounted (at `/dashboard/apps/register`, Wallow-ffpq.3.5)
 * the branding display-name / tagline / logo inputs must be reachable in the
 * form view. Testids follow the component's own `app-*` convention (the
 * `register-app-*` testids were renamed to `app-*`).
 *
 * This is a render-only reachability spec (no query/mutation fires); the SDK
 * client mock is installed only to guard against a real network call
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path).
 */

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("RegisterAppForm branding/logo upsert", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("renders an optional branding display-name input in the form view", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    await expect.element(page.getByTestId("app-branding-display-name")).toBeInTheDocument();
  });

  it("renders an optional branding tagline input", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    await expect.element(page.getByTestId("app-branding-tagline")).toBeInTheDocument();
  });

  it("renders a logo file input for the branding upsert", async () => {
    renderWithClient(newClient(), <RegisterAppForm />);
    await expect.element(page.getByTestId("app-logo-input")).toBeInTheDocument();
  });
});
