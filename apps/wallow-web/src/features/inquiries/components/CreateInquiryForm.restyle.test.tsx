import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "../../../test/catalog-select";
import { installSdkClientMock } from "../../../test/sdk-client-mock";
import {
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { CreateInquiryForm } from "./CreateInquiryForm";

/**
 * Restyle spec for the create-inquiry form (Wallow-urec.4.2). The form's
 * behaviour — every field testid, the required-field validation, the submitted
 * body, and the `inquiry-success` / `inquiry-error` states — stays pinned by the
 * sibling `CreateInquiryForm.test.tsx`, which the restyle must not edit.
 *
 * Two things the restyle owns here:
 *   1. The bare `select`/`textarea` controls carry no classes at all today, so
 *      they render as unstyled browser widgets next to the token-styled `ui`
 *      `Input`s. They adopt the SAME recipe `ui/input.tsx` uses, plus the focus
 *      ring the old Blazor form had (mapped from `ring-[#d4a017]` to `ring-ring`).
 *   2. The success state grows into the recipe's centered card — pig emoji and
 *      all — but keeps its existing sentence VERBATIM as the card heading, the
 *      rule `.4.1` set with `AppList`'s empty state. A restyle adds chrome; it
 *      never rewrites copy.
 *
 * The `ui` `Button` behind `inquiry-submit` is deliberately left alone: its
 * recipe already renders gold (`bg-primary`), and appending `rounded-full px-6`
 * would collide with its own `rounded-md px-3` rather than override it.
 */

/**
 * The shared control recipe — `ui/input.tsx`'s measured string plus the Blazor
 * form's focus ring. Kept local to this spec rather than pushed into
 * `src/test/style-contract.ts`, which is shared with the sibling Phase 4 tasks.
 */
const CONTROL =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring";

/** Every field filled with a valid value, so submit reaches the success state. */
const FILLERS: readonly (() => Promise<void>)[] = [
  () => userEvent.type(page.getByTestId("inquiry-name"), "Ada Lovelace"),
  () => userEvent.type(page.getByTestId("inquiry-email"), "ada@example.com"),
  () => userEvent.type(page.getByTestId("inquiry-phone"), "555-0100"),
  () => userEvent.type(page.getByTestId("inquiry-company"), "Analytical Engines"),
  () => chooseOption("inquiry-project-type", "Web Application"),
  () => chooseOption("inquiry-budget-range", "$15,000 - $50,000"),
  () => chooseOption("inquiry-timeline", "1 - 3 months"),
  () => userEvent.type(page.getByTestId("inquiry-message"), "We need a project dashboard."),
];

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** Render the form and resolve its settled `form` element. */
async function renderForm(): Promise<HTMLElement> {
  renderWithClient(<CreateInquiryForm />);
  return waitForTestId("inquiry-create-form");
}

/** Render the form, submit it with every field valid, and resolve the success state. */
async function renderSubmitted(): Promise<HTMLElement> {
  await renderForm();
  for (const fill of FILLERS) {
    await fill();
  }
  await userEvent.click(page.getByTestId("inquiry-submit"));
  return waitForTestId("inquiry-success");
}

describe("CreateInquiryForm (restyle)", () => {
  beforeEach(() => {
    installSdkClientMock();
  });

  it("seats the form on the padded card surface", async () => {
    const form = await renderForm();

    const surface = parentOf(form);
    expectClasses(surface, "bg-card rounded-lg shadow-sm border border-border p-8");
  });

  it("titles the card with an h2 reading Submit an Inquiry", async () => {
    await renderForm();

    const heading = byTestId("inquiry-create-heading");
    expectTag(heading, "h2");
    expect(heading.textContent).toBe("Submit an Inquiry");
    expectClasses(heading, "text-xl font-semibold text-foreground");
  });

  it("spaces the form's fields evenly", async () => {
    const form = await renderForm();

    expectClasses(form, "space-y-5");
  });

  it("styles every select like the shared text input", async () => {
    await renderForm();

    // Post-migration (Wallow-m5aq.5.3) the selects are catalog `Select`s, so the
    // shared control look now arrives from the component's own trigger recipe
    // rather than from this app hand-copying the input's class string. The
    // OVERLAP is what the restyle promised — same width, radius, border, padding,
    // and type scale — so that is what is asserted; the recipe's own additions
    // (`inline-flex`, `border-input`, the popup-open ring) are the component's
    // business, not this spec's.
    for (const testId of ["inquiry-project-type", "inquiry-budget-range", "inquiry-timeline"]) {
      expectCatalogSelect(testId);
      expectClasses(
        byTestId(testId),
        "w-full rounded-md border bg-background px-3 py-2 text-sm text-foreground",
      );
    }
  });

  it("styles the message textarea like the shared text input", async () => {
    await renderForm();

    const message = byTestId("inquiry-message");
    expectTag(message, "textarea");
    expectClasses(message, CONTROL);
  });

  it("presents the success state as a centered card that keeps its sentence", async () => {
    const success = await renderSubmitted();

    expectClasses(success, "text-center py-6");
    expect(success.textContent).toContain("🐷");

    const heading = within(success, "h2");
    expect(heading.textContent).toBe("Thank you — your inquiry has been submitted.");
    expectClasses(heading, "text-xl font-semibold text-foreground mb-2");
  });

  it("styles the form with theme tokens only", async () => {
    const form = await renderForm();

    expectTokenColorsOnly(parentOf(form));
  });
});
