import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "@bc-solutions-coder/testing/catalog-select";
import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { inquiriesGetAllQueryKey } from "../api";
import { CreateInquiryForm } from "./CreateInquiryForm";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Behaviour spec for the create-inquiry form: the field set, the
 * submitted body, the list sweep, required-field parity with the
 * server, and the RFC 7807 banner.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so the submitted
 * body is read off the recorded request rather than a spy.
 *
 * The testids match the C# E2E `InquiryPage` page object verbatim.
 */

// The full SubmitInquiryBody the form must POST when every field is filled.
const FULL_BODY = {
  name: "Ada Lovelace",
  email: "ada@example.com",
  phone: "555-0100",
  company: "Analytical Engines",
  projectType: "web-app",
  budgetRange: "15k-50k",
  timeline: "1-3-months",
  message: "We need a project dashboard.",
};

/**
 * Per-field fill actions keyed by testid, so a test can leave a single field
 * blank (`fillAllExcept`) to isolate its required-validation behavior. The
 * three project selects are catalog `Select`s, driven by their option's
 * accessible LABEL through `chooseOption` — `userEvent.selectOptions` only
 * drives a native `HTMLSelectElement`.
 */
const FIELD_FILLERS: Record<string, () => Promise<void>> = {
  "inquiry-name": () => userEvent.fill(page.getByTestId("inquiry-name"), FULL_BODY.name),
  "inquiry-email": () => userEvent.fill(page.getByTestId("inquiry-email"), FULL_BODY.email),
  "inquiry-phone": () => userEvent.fill(page.getByTestId("inquiry-phone"), FULL_BODY.phone),
  "inquiry-company": () => userEvent.fill(page.getByTestId("inquiry-company"), FULL_BODY.company),
  "inquiry-project-type": () => chooseOption("inquiry-project-type", "Web Application"),
  "inquiry-budget-range": () => chooseOption("inquiry-budget-range", "$15,000 - $50,000"),
  "inquiry-timeline": () => chooseOption("inquiry-timeline", "1 - 3 months"),
  "inquiry-message": () => userEvent.fill(page.getByTestId("inquiry-message"), FULL_BODY.message),
};

async function fillFullForm() {
  for (const fill of Object.values(FIELD_FILLERS)) {
    await fill();
  }
}

/** Fill every field with a valid value except `skipTestId`, which is left blank. */
async function fillAllExcept(skipTestId: string) {
  for (const [testId, fill] of Object.entries(FIELD_FILLERS)) {
    if (testId !== skipTestId) {
      await fill();
    }
  }
}

/**
 * The fields `SubmitInquiryValidator.cs` marks `.NotEmpty()` beyond the core
 * name/email/message trio — phone plus the three project selects. Company is
 * the ONLY server-nullable field, so it is excluded here.
 */
const SERVER_REQUIRED_SELECT_FIELDS = [
  { skipTestId: "inquiry-phone", errorTestId: "inquiry-phone-error" },
  { skipTestId: "inquiry-project-type", errorTestId: "inquiry-project-type-error" },
  { skipTestId: "inquiry-budget-range", errorTestId: "inquiry-budget-range-error" },
  { skipTestId: "inquiry-timeline", errorTestId: "inquiry-timeline-error" },
] as const;

describe("CreateInquiryForm", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders every inquiry field, select, and the submit button", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect
      .element(page.getByTestId("inquiry-create-heading"))
      .toHaveTextContent("Submit an Inquiry");
    await expect.element(page.getByTestId("inquiry-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-phone")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-company")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-project-type")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-budget-range")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-timeline")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-message")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-submit")).toBeInTheDocument();
  });

  it("submits, POSTing the full SubmitInquiryBody to the inquiries endpoint", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillFullForm();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    expect(harness.last?.path).toBe("/api/v1/inquiries");
    expect(harness.last?.body).toEqual(FULL_BODY);
  });

  it("sweeps the inquiry list query after a successful submit", async () => {
    const { queryClient } = renderWithWallow(<CreateInquiryForm />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await fillFullForm();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expectSwept(invalidateSpy, inquiriesGetAllQueryKey());
  });

  it("shows the success state after a successful submit", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillFullForm();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    const success = page.getByTestId("inquiry-success");
    await expect.element(success).toBeInTheDocument();
    // The submitter's only confirmation, so the wording is pinned here.
    await expect.element(success).toHaveTextContent("🐷");
    await expect.element(success).toHaveTextContent("Thank you — your inquiry has been submitted.");
  });

  it("blocks submit and flags EVERY server-required field when the form is empty", async () => {
    // The client mirrors the server's `.NotEmpty()` set so a user is never told
    // their submission is valid when the server will reject it.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect.element(page.getByTestId("inquiry-name-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-email-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-phone-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-project-type-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-budget-range-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-timeline-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-message-error")).toBeInTheDocument();
    expect(harness.calls).toHaveLength(0);
  });

  it("surfaces the RFC 7807 ProblemDetails detail when the submit fails", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "Bad Request",
        status: "400",
        detail: "Failed to submit inquiry. Please try again.",
      },
      400,
    );

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillFullForm();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-error"))
      .toHaveTextContent("Failed to submit inquiry. Please try again.");
  });
});

describe("CreateInquiryForm — server-required field parity", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it.each(SERVER_REQUIRED_SELECT_FIELDS)(
    "blocks submit and flags $errorTestId when only that server-required field is blank",
    async ({ skipTestId, errorTestId }) => {
      renderWithWallow(<CreateInquiryForm />, { harness });

      await fillAllExcept(skipTestId);
      await userEvent.click(page.getByTestId("inquiry-submit"));

      await expect.element(page.getByTestId(errorTestId)).toBeInTheDocument();
      expect(harness.calls).toHaveLength(0);
    },
  );

  it("still submits when only company (the sole server-optional field) is blank", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillAllExcept("inquiry-company");
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    expect(harness.last?.body).toEqual({ ...FULL_BODY, company: "" });
  });
});
