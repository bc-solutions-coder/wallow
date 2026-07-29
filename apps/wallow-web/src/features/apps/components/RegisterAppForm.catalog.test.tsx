import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "../../../test/catalog-select";
import { byTestId, waitForTestId } from "../../../test/style-contract";
import { RegisterAppForm } from "./RegisterAppForm";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Catalog-migration spec for the register-app form (Wallow-m5aq.5.3) — the two
 * hand-rolled primitives this form owned, and what each must become:
 *
 *   `app-client-type`   raw <select>                    -> catalog `Select`
 *   `app-scope-*`       raw aria-pressed <button> list   -> catalog `ToggleGroup`
 *
 * Every testid is preserved. The scope toggles are the interesting case: they
 * already carried `aria-pressed`, so the gap the migration closes is not the
 * per-button state but the GROUPING — eight independent pressed buttons in a
 * bare `<div>` announce as eight unrelated controls, where a toggle group
 * announces as one multi-select control. That is why these cases assert the
 * group wrapper and the multi-selection semantics, and leave "what a toggled
 * scope does to the submitted body" to `RegisterAppForm.test.tsx`, which already
 * pins it.
 */

const OK_RESPONSE = {
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  registrationAccessToken: "rat-123",
};

/** Every scope the form offers, with the testid it is addressed by. */
const SCOPE_TESTIDS: readonly string[] = [
  "app-scope-inquiries-read",
  "app-scope-inquiries-write",
  "app-scope-announcements-read",
  "app-scope-storage-read",
  "app-scope-openid",
  "app-scope-profile",
  "app-scope-email",
  "app-scope-offline_access",
];

function renderForm(): Promise<HTMLElement> {
  renderWithWallow(<RegisterAppForm />, { harness });
  return waitForTestId("app-register-form");
}

/** The nearest ancestor of `testId` that announces itself as a group. */
function groupContaining(testId: string): HTMLElement | null {
  return byTestId(testId).closest<HTMLElement>('[role="group"]');
}

beforeEach(() => {
  harness = createSdkHarness();
  harness.resolveJson(OK_RESPONSE);
});

describe("RegisterAppForm client type (catalog Select)", () => {
  it("presents the client-type control as a combobox rather than a native select", async () => {
    await renderForm();

    expectCatalogSelect("app-client-type");
  });

  it("offers both client types as named options", async () => {
    await renderForm();

    await userEvent.click(byTestId("app-client-type"));

    await expect.element(page.getByRole("option", { name: "Public", exact: true })).toBeVisible();
    await expect
      .element(page.getByRole("option", { name: "Confidential", exact: true }))
      .toBeVisible();
  });

  it("reports the chosen client type on the trigger", async () => {
    await renderForm();

    await chooseOption("app-client-type", "Confidential");

    await expect.element(page.getByTestId("app-client-type")).toHaveTextContent("Confidential");
  });
});

describe("RegisterAppForm scope toggles (catalog ToggleGroup)", () => {
  it("gathers every scope toggle into one group", async () => {
    await renderForm();

    const group: HTMLElement | null = groupContaining(SCOPE_TESTIDS[0] as string);
    expect(group, "the scope toggles must live inside a role=group container").not.toBeNull();

    // ONE group, not one per toggle: the whole point is that these eight controls
    // are announced as a single multi-select, so they must share a wrapper.
    for (const testId of SCOPE_TESTIDS) {
      await expect.element(page.getByTestId(testId)).toBeInTheDocument();
      expect(groupContaining(testId)).toBe(group);
    }
  });

  it("keeps each scope a pressed-state toggle button", async () => {
    await renderForm();

    for (const testId of SCOPE_TESTIDS) {
      const toggle: HTMLElement = byTestId(testId);
      expect(toggle.tagName).toBe("BUTTON");
      expect(toggle.hasAttribute("aria-pressed"), `${testId} must report aria-pressed`).toBe(true);
    }
  });

  it("starts with only the default scope pressed", async () => {
    await renderForm();

    const pressed: string[] = SCOPE_TESTIDS.filter(
      (testId) => byTestId(testId).getAttribute("aria-pressed") === "true",
    );
    expect(pressed).toEqual(["app-scope-inquiries-read"]);
  });

  it("holds several scopes pressed at once", async () => {
    await renderForm();

    // Multi-selection, not single: pressing a second scope must not release the
    // first, which is the one behaviour a single-selection toggle group would
    // silently break.
    await userEvent.click(byTestId("app-scope-storage-read"));
    await userEvent.click(byTestId("app-scope-openid"));

    for (const testId of [
      "app-scope-inquiries-read",
      "app-scope-storage-read",
      "app-scope-openid",
    ]) {
      await expect.element(page.getByTestId(testId)).toHaveAttribute("aria-pressed", "true");
    }
  });

  it("releases a pressed scope when it is activated again", async () => {
    await renderForm();

    await userEvent.click(byTestId("app-scope-inquiries-read"));

    await expect
      .element(page.getByTestId("app-scope-inquiries-read"))
      .toHaveAttribute("aria-pressed", "false");
  });
});
