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
import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * Restyle spec for the inline create-organization form (Wallow-urec.4.3). It
 * asserts only the chrome the restyle adds; the form's behaviour (validation,
 * submit payload, reset-on-success, the ProblemDetails surface, and the
 * `organization-name` / `organization-create-submit` / `organization-name-error`
 * / `organization-create-error` testids) stays pinned by
 * `CreateOrganizationForm.test.tsx`, which the restyle must not edit.
 *
 * Unlike a list, a form KEEPS the `ui` `Card` — nothing inside it fights the
 * card's `p-6 space-y-6` (Wallow-urec.4.1, rule: raw div for tables, Card for
 * forms). The restyle therefore only titles the card, spaces the form body, and
 * separates the card from the list it sits under on the organizations index
 * page (this form has exactly one mount site, so it owns that top margin).
 *
 * New testid (pure addition — no spec pins a heading here today):
 * `organization-create-heading`.
 */

function renderForm(): Promise<HTMLElement> {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const ui: ReactElement = (
    <QueryClientProvider client={client}>
      <CreateOrganizationForm />
    </QueryClientProvider>
  );
  render(ui);
  return waitForTestId("organization-create-form");
}

describe("CreateOrganizationForm (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("separates the form card from the list above it", async () => {
    const form = await renderForm();

    const surface = parentOf(form);
    expectTag(surface, "div");
    expectClasses(surface, "rounded-lg border border-border bg-card mt-8");
  });

  it("titles the card with a Create Organization heading", async () => {
    await renderForm();

    const heading = byTestId("organization-create-heading");
    expectTag(heading, "h2");
    expect(heading.textContent).toBe("Create Organization");
    expectClasses(heading, "text-lg font-semibold text-card-foreground");
  });

  it("renders the heading above the form", async () => {
    const form = await renderForm();

    expectPrecedes(byTestId("organization-create-heading"), form);
  });

  it("spaces the form body on the shared rhythm", async () => {
    const form = await renderForm();

    expectClasses(form, "space-y-6");
  });

  it("styles the form with theme tokens only", async () => {
    const form = await renderForm();

    expectTokenColorsOnly(parentOf(form));
  });
});
