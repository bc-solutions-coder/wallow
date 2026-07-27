import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "../../../test/catalog-select";
import { installSdkClientMock } from "../../../test/sdk-client-mock";
import { byTestId, waitForTestId } from "../../../test/style-contract";
import { CreateInquiryForm } from "./CreateInquiryForm";

/**
 * Catalog-migration spec for the create-inquiry form (Wallow-m5aq.5.3). The form
 * renders THREE selects through one shared `SelectField`, so all three migrate
 * together: `inquiry-project-type`, `inquiry-budget-range`, `inquiry-timeline`.
 * Every testid is preserved and now names the catalog `Select`'s trigger.
 *
 * The wire values are cosmetic-to-display pairs (`web-app` -> "Web Application"),
 * which is the reason these cases assert the visible LABEL is what a user picks
 * while `CreateInquiryForm.test.tsx` keeps asserting the VALUE that reaches the
 * submitted body. A migration that dropped the label mapping would pass one and
 * fail the other.
 */

/** Each migrated select with one of its options, by the label a user reads. */
const SELECTS: ReadonlyArray<readonly [testId: string, optionLabel: string]> = [
  ["inquiry-project-type", "Web Application"],
  ["inquiry-budget-range", "$15,000 - $50,000"],
  ["inquiry-timeline", "1 - 3 months"],
];

function renderForm(): Promise<HTMLElement> {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const ui: ReactElement = (
    <QueryClientProvider client={client}>
      <CreateInquiryForm />
    </QueryClientProvider>
  );
  render(ui);
  return waitForTestId("inquiry-create-form");
}

beforeEach(() => {
  installSdkClientMock();
});

describe("CreateInquiryForm selects (catalog Select)", () => {
  it("presents every project select as a combobox rather than a native select", async () => {
    await renderForm();

    for (const [testId] of SELECTS) {
      expectCatalogSelect(testId);
    }
  });

  it("offers each select's options only once it is opened", async () => {
    await renderForm();

    // Nothing of the list exists up front — the catalog Select portals its popup
    // and mounts it on demand, so an unopened select contributes no options to
    // the page at all.
    await expect.element(page.getByRole("option")).not.toBeInTheDocument();

    await userEvent.click(byTestId("inquiry-project-type"));

    await expect
      .element(page.getByRole("option", { name: "Web Application", exact: true }))
      .toBeVisible();
  });

  /**
   * The full option set, per select. This replaces the pre-migration
   * `querySelectorAll("option")` case in `CreateInquiryForm.test.tsx`, which no
   * conforming implementation could satisfy: the catalog `Select` renders
   * `role="option"` divs portalled to `<body>` and only while open, never a
   * native `<option>`. The wire values behind these labels stay covered by that
   * spec's submitted-body assertion.
   */
  const OPTION_LABELS: ReadonlyArray<readonly [testId: string, labels: readonly string[]]> = [
    [
      "inquiry-project-type",
      [
        "Web Application",
        "Mobile Application",
        "API / Backend",
        "SaaS Platform",
        "Consulting",
        "Other",
      ],
    ],
    [
      "inquiry-budget-range",
      ["Under $5,000", "$5,000 - $15,000", "$15,000 - $50,000", "$50,000 - $100,000", "$100,000+"],
    ],
    ["inquiry-timeline", ["ASAP", "1 - 3 months", "3 - 6 months", "6+ months", "Flexible"]],
  ];

  it("lists every option, by label, for each opened select", async () => {
    await renderForm();

    for (const [testId, labels] of OPTION_LABELS) {
      const trigger: HTMLElement = byTestId(testId);

      await userEvent.click(trigger);
      await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("true");

      // One poll for the whole listbox: only one popup is open at a time, so
      // `role=option` is unambiguous, and the exact array pins the set — no
      // strays, right order — as well as the labels.
      await expect
        .poll(() =>
          page
            .getByRole("option")
            .elements()
            .map((o) => o.textContent?.trim()),
        )
        .toEqual([...labels]);

      // Close by choosing an option, the path `chooseOption` uses; an
      // overlapping popup would make the next query ambiguous.
      await userEvent.click(page.getByRole("option", { name: labels[0], exact: true }));
      await expect.poll(() => trigger.getAttribute("aria-expanded")).toBe("false");
    }
  });

  it("reports the chosen option's label on each trigger", async () => {
    await renderForm();

    for (const [testId, optionLabel] of SELECTS) {
      await chooseOption(testId, optionLabel);
      await expect.element(page.getByTestId(testId)).toHaveTextContent(optionLabel);
    }
  });
});
